using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskApi.Controllers
{
    [ApiController]
    [Route("tasks")]
    [Produces("application/json")]
    public sealed class TasksController : ControllerBase
    {
        readonly TaskDbContext db;
        readonly ITaskEventPublisher publisher;

        public TasksController(TaskDbContext db, ITaskEventPublisher publisher)
        {
            this.db = db;
            this.publisher = publisher;
        }

        [HttpPost]
        [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskResponse>> CreateAsync(
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            Dictionary<string, string[]> errors = Validate(request);
            if (errors.Count > 0)
            {
                return BadRequest(new ValidationProblemDetails(errors));
            }

            TaskItem task = new()
            {
                Id = Guid.NewGuid(),
                Title = request.Title!.Trim(),
                IsCompleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                CompletedAt = null,
                Priority = request.Priority ?? Priority.Medium
            };

            db.Tasks.Add(task);
            await db.SaveChangesAsync(cancellationToken);

            return Created($"/tasks/{task.Id}", ToResponse(task));
        }

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<TaskResponse>>> GetAsync(
            CancellationToken cancellationToken)
        {
            List<TaskResponse> tasks = await db.Tasks
                .AsNoTracking()
                .OrderBy(task => task.CreatedAt)
                .Select(task => new TaskResponse(
                    task.Id,
                    task.Title,
                    task.IsCompleted,
                    task.CreatedAt,
                    task.CompletedAt,
                    task.Priority))
                .ToListAsync(cancellationToken);

            return Ok(tasks);
        }

        [HttpPut("{id:guid}/complete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CompleteAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            TaskItem? task = await db.Tasks.SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

            if (task is null)
            {
                return NotFound();
            }

            if (task.IsCompleted)
            {
                return Conflict(CreateProblem("Task is already completed."));
            }

            task.IsCompleted = true;
            task.CompletedAt = DateTimeOffset.UtcNow;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(CreateProblem("Task was completed by another request."));
            }

            TaskCompletedEvent message = new(
                task.Id,
                task.Title,
                task.CompletedAt.Value,
                task.Priority);
            await publisher.PublishAsync(message);

            return Ok(ToResponse(task));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            TaskItem? task = await db.Tasks.SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

            if (task is null)
            {
                return NotFound();
            }

            db.Tasks.Remove(task);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(CreateProblem("Task was changed by another request."));
            }

            return Ok(ToResponse(task));
        }

        static Dictionary<string, string[]> Validate(CreateTaskRequest request)
        {
            Dictionary<string, string[]> errors = new();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors[nameof(request.Title)] = ["Title must not be empty."];
            }
            else if (request.Title.Length > 200)
            {
                errors[nameof(request.Title)] = ["Title must not exceed 200 characters."];
            }

            if (request.Priority is not null && !Enum.IsDefined(request.Priority.Value))
            {
                errors[nameof(request.Priority)] = ["Priority must be Low, Medium, or High."];
            }

            return errors;
        }

        static ProblemDetails CreateProblem(string detail)
            =>
                new()
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = detail
                };

        static TaskResponse ToResponse(TaskItem task)
            =>
                new(
                    task.Id,
                    task.Title,
                    task.IsCompleted,
                    task.CreatedAt,
                    task.CompletedAt,
                    task.Priority);
    }
}

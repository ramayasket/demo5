using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskApi;
using Xunit;

namespace TaskApi.Tests
{
    public sealed class CompleteTaskIntegrationTest
    {
        [Fact]
        public async Task CompletePublishesRmqEvent()
        {
            FakePublisher publisher = new();
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            string database = Guid.NewGuid().ToString();

            using (WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddSimpleConsole();
                    });
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<TaskDbContext>>();
                        services.RemoveAll<ITaskEventPublisher>();
                        services.AddDbContext<TaskDbContext>(options =>
                            options.UseInMemoryDatabase(database));
                        services.AddSingleton<ITaskEventPublisher>(publisher);
                    });
                }))

            using (HttpClient client = factory.CreateClient())
            {
                HttpResponseMessage created = await client.PostAsJsonAsync(
                    "/tasks",
                    new { title = "Buy milk", priority = "High" },
                    cancellationToken);

                string body = await created.Content.ReadAsStringAsync(cancellationToken);
                Assert.True(created.IsSuccessStatusCode, body);

                TaskResponse? task = await created.Content.ReadFromJsonAsync<TaskResponse>(cancellationToken);
                Assert.NotNull(task);
                Assert.False(task.IsCompleted);
                Assert.Null(task.CompletedAt);

                HttpResponseMessage completed = await client.PutAsync(
                    $"/tasks/{task.Id}/complete",
                    null,
                    cancellationToken);

                Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
                TaskCompletedEvent message = Assert.Single(publisher.Messages);
                Assert.Equal(task.Id, message.TaskId);
                Assert.Equal("Buy milk", message.Title);
                Assert.Equal(Priority.High, message.Priority);
                Assert.NotEqual(default, message.CompletedAt);
            }
        }

        sealed class FakePublisher : ITaskEventPublisher
        {
            public List<TaskCompletedEvent> Messages { get; } = [];

            public ValueTask PublishAsync(TaskCompletedEvent message)
            {
                Messages.Add(message);
                return ValueTask.CompletedTask;
            }
        }
    }
}

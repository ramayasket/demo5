using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace TaskApi
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            if (!builder.Environment.IsEnvironment("Testing"))
            {
                string connection = builder.Configuration.GetConnectionString("Tasks")
                    ?? throw new InvalidOperationException(
                        "Connection string 'Tasks' is not configured.");
                builder.Services.AddDbContext<TaskDbContext>(options =>
                    options.UseNpgsql(connection));
            }

            builder.Services.Configure<RmqOptions>(builder.Configuration.GetSection("RabbitMq"));
            builder.Services.AddSingleton<RmqPublisher>();
            builder.Services.AddSingleton<ITaskEventPublisher>(services =>
                services.GetRequiredService<RmqPublisher>());
            builder.Services.AddSingleton<IHostedService>(services =>
                services.GetRequiredService<RmqPublisher>());
            builder.Services.AddControllers().AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddProblemDetails();
            builder.Services.AddSwaggerGen(options =>
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Task API",
                    Version = "v1"
                }));

            WebApplication app = builder.Build();

            if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseExceptionHandler();

                await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
                {
                    TaskDbContext db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
                    await db.Database.MigrateAsync();
                }
            }

            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapControllers();

            await app.RunAsync();
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TaskApi
{
    public sealed class RmqPublisher : ITaskEventPublisher, IHostedService, IAsyncDisposable
    {
        static readonly JsonSerializerOptions json = CreateJsonOptions();

        readonly RmqOptions options;
        readonly ILogger<RmqPublisher> logger;
        readonly Channel<TaskCompletedEvent> queue;

        CancellationTokenSource? stop;
        Task? worker;
        IConnection? connection;
        IChannel? channel;

        public RmqPublisher(
            IOptions<RmqOptions> options,
            ILogger<RmqPublisher> logger)
        {
            this.options = options.Value;
            this.logger = logger;
            queue = Channel.CreateUnbounded<TaskCompletedEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            stop = new CancellationTokenSource();
            worker = RunAsync(stop.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            queue.Writer.TryComplete();

            if (worker is not null)
            {
                TimeSpan timeout = TimeSpan.FromSeconds(options.ShutdownTimeoutSeconds);
                Task finished = await Task.WhenAny(worker, Task.Delay(timeout, cancellationToken));

                if (finished != worker)
                {
                    logger.LogWarning(
                        "RabbitMQ publisher did not drain within {TimeoutSeconds} seconds.",
                        options.ShutdownTimeoutSeconds);
                    stop?.Cancel();
                }

                try
                {
                    await worker;
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("RabbitMQ publisher stopped before all messages were sent.");
                }
            }

            await CloseAsync();
        }

        public ValueTask PublishAsync(TaskCompletedEvent message)
        {
            if (!queue.Writer.TryWrite(message))
            {
                logger.LogError(
                    "RabbitMQ message for task {TaskId} was not queued because the publisher is stopping.",
                    message.TaskId);
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource? source = Interlocked.Exchange(ref stop, null);
            if (source is not null)
            {
                source.Cancel();
                source.Dispose();
            }

            await CloseAsync();
        }

        async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (TaskCompletedEvent message in queue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await SendAsync(message, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "RabbitMQ message for task {TaskId} could not be published.",
                        message.TaskId);
                    await CloseAsync();
                }
            }
        }

        async Task SendAsync(TaskCompletedEvent message, CancellationToken cancellationToken)
        {
            await ConnectAsync(cancellationToken);

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message, json);
            await channel!.BasicPublishAsync(
                options.Exchange,
                options.RoutingKey,
                body,
                cancellationToken);
        }

        async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (connection?.IsOpen == true && channel?.IsOpen == true)
            {
                return;
            }

            await CloseAsync();

            ConnectionFactory factory = new()
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                AutomaticRecoveryEnabled = true
            };

            connection = await factory.CreateConnectionAsync(cancellationToken);
            channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(
                options.Exchange,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        async ValueTask CloseAsync()
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
                channel = null;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
                connection = null;
            }
        }

        static JsonSerializerOptions CreateJsonOptions()
        {
            JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}

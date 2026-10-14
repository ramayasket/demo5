namespace TaskApi
{
    public sealed class RmqOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string Username { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public string Exchange { get; set; } = "task.events";

        public string RoutingKey { get; set; } = "task.completed";

        public int ShutdownTimeoutSeconds { get; set; } = 5;
    }
}

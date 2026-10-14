using System.Text.Json.Serialization;

namespace TaskApi
{
    public sealed class TaskItem
    {
        public Guid Id { get; init; }

        public required string Title { get; init; }

        public bool IsCompleted { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Priority Priority { get; init; }

        public uint RowVersion { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<Priority>))]
    public enum Priority
    {
        Low,
        Medium,
        High
    }
}

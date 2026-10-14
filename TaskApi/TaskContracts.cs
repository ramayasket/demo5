namespace TaskApi
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed record CreateTaskRequest(string? Title, Priority? Priority);

    public sealed record TaskResponse(
        Guid Id,
        string Title,
        bool IsCompleted,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        Priority Priority);

    public sealed record TaskCompletedEvent(
        Guid TaskId,
        string Title,
        DateTimeOffset CompletedAt,
        Priority Priority);
}

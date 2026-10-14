namespace TaskApi
{
    public interface ITaskEventPublisher
    {
        ValueTask PublishAsync(TaskCompletedEvent message);
    }
}

namespace CQBus.Mediator.Executors;

public interface IExecutorFactory
{
    IRequestExecutor Request { get; }
    INotificationExecutor Notification { get; }
    IStreamExecutor Stream { get; }
}

internal sealed class ExecutorFactory : IExecutorFactory
{
    public ExecutorFactory(
        IRequestExecutor request,
        INotificationExecutor notification,
        IStreamExecutor stream)
    {
        Request = request;
        Notification = notification;
        Stream = stream;
    }

    public IRequestExecutor Request { get; }
    public INotificationExecutor Notification { get; }
    public IStreamExecutor Stream { get; }
}

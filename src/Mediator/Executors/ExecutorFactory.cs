namespace CQBus.Mediator.Executors;

public interface IExecutorFactory
{
    IRequestExecutor Request { get; }
    INotificationExecutor Notification { get; }
    IStreamExecutor Stream { get; }
}

internal sealed class ExecutorFactory(
    IRequestExecutor request,
    INotificationExecutor notification,
    IStreamExecutor stream) : IExecutorFactory
{
    public IRequestExecutor Request { get; } = request;
    public INotificationExecutor Notification { get; } = notification;
    public IStreamExecutor Stream { get; } = stream;
}

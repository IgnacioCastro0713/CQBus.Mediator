using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;

namespace CQBus.Mediator.NotificationPublishers;

public class TaskWhenAllPublisher : INotificationPublisher
{
    public ValueTask Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        IEnumerable<Task> tasks = handlers.Select(handler => handler.Handle(notification, cancellationToken).AsTask());

        var allTasks = Task.WhenAll(tasks);

        return new ValueTask(allTasks);
    }
}

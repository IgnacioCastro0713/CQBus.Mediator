using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;

namespace CQBus.Mediator.NotificationPublishers;

public class TaskWhenAllPublisher : INotificationPublisher
{
    public async ValueTask Publish<TNotification>(
        INotificationHandler<TNotification>[] handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        Task[] tasks = handlers.Select(handler => handler.Handle(notification, cancellationToken).AsTask()).ToArray();

        await Task.WhenAll(tasks);
    }
}

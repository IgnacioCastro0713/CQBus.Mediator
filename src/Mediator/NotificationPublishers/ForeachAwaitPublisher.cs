using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;

namespace CQBus.Mediator.NotificationPublishers;

public class ForeachAwaitPublisher : INotificationPublisher
{
    public async ValueTask Publish<TNotification>(
        INotificationHandler<TNotification>[] handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (INotificationHandler<TNotification> handler in handlers)
        {
            await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

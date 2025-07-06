using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;

namespace CQBus.Mediator.NotificationPublishers;

public interface INotificationPublisher
{
    ValueTask Publish<TNotification>(
        INotificationHandler<TNotification>[] handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification;
}

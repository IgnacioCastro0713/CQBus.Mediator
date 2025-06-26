using CQBus.Mediator.Messages;

namespace CQBus.Mediator.Handlers;

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}

public abstract class NotificationHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    ValueTask INotificationHandler<TNotification>.Handle(TNotification notification, CancellationToken cancellationToken)
    {
        Handle(notification);

        return ValueTask.CompletedTask;
    }

    protected abstract void Handle(TNotification notification);
}


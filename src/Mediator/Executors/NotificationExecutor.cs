using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.Executors;

public interface INotificationExecutor
{
    ValueTask Execute<TNotification>(
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}

internal sealed class NotificationExecutor(IServiceProvider serviceProvider) : INotificationExecutor
{
    public ValueTask Execute<TNotification>(
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        IEnumerable<INotificationHandler<TNotification>> enumerable =
            serviceProvider.GetServices<INotificationHandler<TNotification>>();

        INotificationHandler<TNotification>[] handlers = enumerable switch
        {
            INotificationHandler<TNotification>[] arr => arr,
            _ => enumerable.ToArray()
        };

        return publisher.Publish(handlers, notification, cancellationToken);
    }
}

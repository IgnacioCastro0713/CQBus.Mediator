using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface INotificationPipelineBuilder
{
    ValueTask Execute<TNotification>(
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}

internal sealed class NotificationPipelineBuilder(IServiceProvider serviceProvider) : INotificationPipelineBuilder
{
    public ValueTask Execute<TNotification>(
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        IEnumerable<INotificationHandler<TNotification>> enumerable = serviceProvider.GetServices<INotificationHandler<TNotification>>();
        INotificationHandler<TNotification>[] handlers = enumerable switch
        {
            INotificationHandler<TNotification>[] arr => arr,
            List<INotificationHandler<TNotification>> list => list.ToArray(),
            _ => enumerable.ToArray()
        };

        return publisher.Publish(handlers, notification, cancellationToken);
    }
}

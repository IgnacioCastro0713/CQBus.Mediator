using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface INotificationPipelineBuilder
{
    ValueTask BuildAndExecute<TNotification>(
        TNotification notification,
        IServiceProvider services,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}

internal class NotificationPipelineBuilder : INotificationPipelineBuilder
{
    public ValueTask BuildAndExecute<TNotification>(
        TNotification notification,
        IServiceProvider services,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        IEnumerable<INotificationHandler<TNotification>> handlers = services.GetServices<INotificationHandler<TNotification>>();

        return publisher.Publish(handlers, notification, cancellationToken);
    }
}

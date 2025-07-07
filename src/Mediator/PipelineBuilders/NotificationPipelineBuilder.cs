using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface INotificationPipelineBuilder
{
    ValueTask Execute<TNotification>(
        TNotification notification,
        IServiceProvider services,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}

internal sealed class NotificationPipelineBuilder : INotificationPipelineBuilder
{
    public static NotificationPipelineBuilder Instance { get; } = new();

    public ValueTask Execute<TNotification>(
        TNotification notification,
        IServiceProvider services,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        INotificationHandler<TNotification>[] handlers = services.GetServices<INotificationHandler<TNotification>>().ToArray();

        return publisher.Publish(handlers, notification, cancellationToken);
    }
}

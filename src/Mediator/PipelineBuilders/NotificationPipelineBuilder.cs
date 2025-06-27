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

internal sealed class NotificationPipelineBuilder : INotificationPipelineBuilder
{
    public ValueTask BuildAndExecute<TNotification>(
        TNotification notification,
        IServiceProvider services,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        using IEnumerator<INotificationHandler<TNotification>> handlersEnumerator = services.GetServices<INotificationHandler<TNotification>>().GetEnumerator();
        if (!handlersEnumerator.MoveNext())
        {
            return default;
        }

        INotificationHandler<TNotification> firstHandler = handlersEnumerator.Current;

        if (!handlersEnumerator.MoveNext())
        {
            return firstHandler.Handle(notification, cancellationToken);
        }

        List<INotificationHandler<TNotification>> handlersList =
        [
            firstHandler,
            handlersEnumerator.Current
        ];

        while (handlersEnumerator.MoveNext())
        {
            handlersList.Add(handlersEnumerator.Current);
        }

        return publisher.Publish(handlersList, notification, cancellationToken);
    }
}

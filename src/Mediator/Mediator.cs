using CQBus.Mediator.Executors;
using CQBus.Mediator.Invokers;
using CQBus.Mediator.Maps;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;

namespace CQBus.Mediator;

public sealed class Mediator(
    IExecutorFactory factory,
    IMediatorDispatchMaps maps,
    INotificationPublisher publisher) : IMediator
{
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));

        if (!maps.Requests.TryGetValue(key, out Delegate? del) || del is not RequestInvoker<TResponse> handler)
        {
            throw new InvalidOperationException($"No IRequest handler map for ({key.req.FullName ?? key.req.Name}, {key.res.FullName ?? key.res.Name}).");
        }

        return handler(factory.Request, request, cancellationToken);
    }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        Type key = notification.GetType();

        if (!maps.Notifications.TryGetValue(key, out Delegate? del) || del is not NotificationInvoker<TNotification> handler)
        {
            return ValueTask.CompletedTask;
        }

        return handler(factory.Notification, notification, publisher, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));

        if (!maps.Streams.TryGetValue(key, out Delegate? del) || del is not StreamInvoker<TResponse> handler)
        {
            throw new InvalidOperationException($"No IStream handler map for ({key.req.FullName ?? key.req.Name}, {key.res.FullName ?? key.res.Name}).");
        }

        return handler(factory.Stream, request, cancellationToken);
    }
}

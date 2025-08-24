using CQBus.Mediator.Maps;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;

namespace CQBus.Mediator;

public sealed class Mediator(
    IServiceProvider serviceProvider,
    IMediatorDispatchMaps maps,
    INotificationPublisher publisher) : IMediator
{
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));
        if (!maps.Requests.TryGetValue(key, out Delegate? del))
        {
            throw new InvalidOperationException($"No IRequest handler map for ({key.req.Name}, {key.res.Name}).");
        }

        var handler = (RequestInvoker<TResponse>)del;
        return handler(RequestPipelineBuilder.Instance, request, serviceProvider, cancellationToken);
    }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!maps.Notifications.TryGetValue(notification.GetType(), out Delegate? del))
        {
            return ValueTask.CompletedTask;
        }

        var handler = (NotificationInvoker<TNotification>)del;
        return handler(NotificationPipelineBuilder.Instance, notification, serviceProvider, publisher, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));
        if (!maps.Streams.TryGetValue(key, out Delegate? del))
        {
            throw new InvalidOperationException($"No IStream handler map for ({key.req.Name}, {key.res.Name}).");
        }

        var handler = (StreamInvoker<TResponse>)del;
        return handler(StreamPipelineBuilder.Instance, request, serviceProvider, cancellationToken);
    }
}

internal delegate ValueTask<TResponse> RequestInvoker<TResponse>(
    RequestPipelineBuilder pb, IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);

internal delegate ValueTask NotificationInvoker<in TNotification>(
    NotificationPipelineBuilder pb, TNotification notification, IServiceProvider sp, INotificationPublisher publisher, CancellationToken ct)
    where TNotification : INotification;

internal delegate IAsyncEnumerable<TResponse> StreamInvoker<TResponse>(
    StreamPipelineBuilder pb, IStreamRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);

using System.Collections.Frozen;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;

namespace CQBus.Mediator;

public sealed class Mediator(
    IServiceProvider serviceProvider,
    IMediatorDispatchMaps maps,
    INotificationPublisher? publisher = null) : IMediator
{
    private readonly INotificationPublisher _publisher = publisher ?? new ForeachAwaitPublisher();

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));
        if (!maps.Requests.TryGetValue(key, out Delegate? del))
        {
            throw new InvalidOperationException($"No IRequest handler map for ({key.req.Name}, {key.res.Name}).");
        }

        var invoker = (RequestInvoker<TResponse>)del;
        return invoker(RequestPipelineBuilder.Instance, request, serviceProvider, cancellationToken);
    }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!maps.Notifications.TryGetValue(notification.GetType(), out Delegate? del))
        {
            return ValueTask.CompletedTask;
        }

        var invoker = (NotificationInvoker<TNotification>)del;
        return invoker(NotificationPipelineBuilder.Instance, notification, serviceProvider, _publisher, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        (Type req, Type res) key = (request.GetType(), typeof(TResponse));
        if (!maps.Streams.TryGetValue(key, out Delegate? del))
        {
            throw new InvalidOperationException($"No IStream handler map for ({key.req.Name}, {key.res.Name}).");
        }

        var invoker = (StreamInvoker<TResponse>)del;
        return invoker(StreamPipelineBuilder.Instance, request, serviceProvider, cancellationToken);
    }
}

internal delegate ValueTask<TResponse> RequestInvoker<TResponse>(
    IRequestPipelineBuilder pb, IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);

internal delegate ValueTask NotificationInvoker<in TNotification>(
    INotificationPipelineBuilder pb, TNotification notification, IServiceProvider sp, INotificationPublisher publisher, CancellationToken ct)
    where TNotification : INotification;

internal delegate IAsyncEnumerable<TResponse> StreamInvoker<TResponse>(
    IStreamPipelineBuilder pb, IStreamRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);



internal static class StaticInvoker
{
    public static ValueTask<TResponse> Request<TRequest, TResponse>(
        IRequestPipelineBuilder pipelineBuilder,
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, serviceProvider, cancellationToken);

    public static ValueTask Notification<TNotification>(
        INotificationPipelineBuilder pipelineBuilder,
        TNotification notification,
        IServiceProvider serviceProvider,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
        => pipelineBuilder.Execute(notification, serviceProvider, publisher, cancellationToken);

    public static IAsyncEnumerable<TResponse> Stream<TRequest, TResponse>(
        IStreamPipelineBuilder pipelineBuilder,
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, serviceProvider, cancellationToken);
}

public interface IMediatorDispatchMaps
{
    FrozenDictionary<(Type req, Type res), Delegate> Requests { get; }
    FrozenDictionary<Type, Delegate> Notifications { get; }
    FrozenDictionary<(Type req, Type res), Delegate> Streams { get; }
}

internal sealed class MediatorDispatchMaps(
    FrozenDictionary<(Type, Type), Delegate> requests,
    FrozenDictionary<Type, Delegate> notifications,
    FrozenDictionary<(Type, Type), Delegate> streams) : IMediatorDispatchMaps
{
    public FrozenDictionary<(Type, Type), Delegate> Requests { get; } = requests;
    public FrozenDictionary<Type, Delegate> Notifications { get; } = notifications;
    public FrozenDictionary<(Type, Type), Delegate> Streams { get; } = streams;
}


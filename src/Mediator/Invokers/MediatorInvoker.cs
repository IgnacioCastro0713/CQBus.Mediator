using CQBus.Mediator.Executors;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;

namespace CQBus.Mediator.Invokers;

internal static class MediatorInvoker
{
    public static ValueTask<TResponse> Request<TRequest, TResponse>(
        IRequestExecutor executor,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
        => executor.Execute<TRequest, TResponse>((TRequest)request, cancellationToken);

    public static ValueTask Notification<TNotification>(
        INotificationExecutor executor,
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
        => executor.Execute(notification, publisher, cancellationToken);

    public static IAsyncEnumerable<TResponse> Stream<TRequest, TResponse>(
        IStreamExecutor executor,
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
        => executor.Execute<TRequest, TResponse>((TRequest)request, cancellationToken);
}


internal delegate ValueTask<TResponse> RequestInvoker<TResponse>(
    IRequestExecutor exc, IRequest<TResponse> request, CancellationToken ct);

internal delegate ValueTask NotificationInvoker<in TNotification>(
    INotificationExecutor exc, TNotification notification, INotificationPublisher publisher, CancellationToken ct)
    where TNotification : INotification;

internal delegate IAsyncEnumerable<TResponse> StreamInvoker<TResponse>(
    IStreamExecutor exc, IStreamRequest<TResponse> request, CancellationToken ct);

using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;

namespace CQBus.Mediator.Invokers;

internal static class MediatorInvoker
{
    public static ValueTask<TResponse> Request<TRequest, TResponse>(
        IRequestPipelineBuilder pipelineBuilder,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, cancellationToken);

    public static ValueTask Notification<TNotification>(
        INotificationPipelineBuilder pipelineBuilder,
        TNotification notification,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
        => pipelineBuilder.Execute(notification, publisher, cancellationToken);

    public static IAsyncEnumerable<TResponse> Stream<TRequest, TResponse>(
        IStreamPipelineBuilder pipelineBuilder,
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, cancellationToken);
}


internal delegate ValueTask<TResponse> RequestInvoker<TResponse>(
    IRequestPipelineBuilder pb, IRequest<TResponse> request, CancellationToken ct);

internal delegate ValueTask NotificationInvoker<in TNotification>(
    INotificationPipelineBuilder pb, TNotification notification, INotificationPublisher publisher, CancellationToken ct)
    where TNotification : INotification;

internal delegate IAsyncEnumerable<TResponse> StreamInvoker<TResponse>(
    IStreamPipelineBuilder pb, IStreamRequest<TResponse> request, CancellationToken ct);

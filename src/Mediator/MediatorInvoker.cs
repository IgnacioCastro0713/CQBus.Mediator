using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;

namespace CQBus.Mediator;

internal static class MediatorInvoker
{
    public static ValueTask<TResponse> Request<TRequest, TResponse>(
        RequestPipelineBuilder pipelineBuilder,
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, serviceProvider, cancellationToken);

    public static ValueTask Notification<TNotification>(
        NotificationPipelineBuilder pipelineBuilder,
        TNotification notification,
        IServiceProvider serviceProvider,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
        where TNotification : INotification
        => pipelineBuilder.Execute(notification, serviceProvider, publisher, cancellationToken);

    public static IAsyncEnumerable<TResponse> Stream<TRequest, TResponse>(
        StreamPipelineBuilder pipelineBuilder,
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
        => pipelineBuilder.Execute<TRequest, TResponse>((TRequest)request, serviceProvider, cancellationToken);
}

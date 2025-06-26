using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;

namespace API.Behaviors;

public sealed class StreamLoggingBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        logger.LogInformation("Stream Handling: {RequestName} {Request}", requestName, request);

        IAsyncEnumerable<TResponse> response = next(cancellationToken);

        logger.LogInformation("Stream Handled: {RequestName} {Request}", requestName, request);

        return response;
    }
}

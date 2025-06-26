using CQBus.Mediator.Pipelines;

namespace API.Behaviors;

public sealed class StreamUnhandledExceptionBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return next(cancellationToken);
        }
        catch (Exception ex)
        {
            string requestName = typeof(TRequest).Name;

            logger.LogError(ex, "Unhandled Exception for {Name} {@Request}", requestName, request);

            throw;
        }
    }
}

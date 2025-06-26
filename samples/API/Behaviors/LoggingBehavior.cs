using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;

namespace API.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling: {RequestName} {Request}", requestName, request);

        TResponse response = await next(cancellationToken);

        logger.LogInformation("Handled: {RequestName} {Request}", requestName, request);

        return response;
    }
}

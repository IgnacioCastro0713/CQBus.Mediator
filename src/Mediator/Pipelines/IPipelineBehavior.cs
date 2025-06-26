namespace CQBus.Mediator.Pipelines;

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

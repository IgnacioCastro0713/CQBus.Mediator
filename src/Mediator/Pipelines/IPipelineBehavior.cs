namespace CQBus.Mediator.Pipelines;

public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken token = default);

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

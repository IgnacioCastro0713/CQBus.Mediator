namespace CQBus.Mediator.Pipelines;

public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken token = default);

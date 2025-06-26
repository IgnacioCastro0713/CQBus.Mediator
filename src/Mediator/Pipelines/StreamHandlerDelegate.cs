namespace CQBus.Mediator.Pipelines;

public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>(CancellationToken token = default);

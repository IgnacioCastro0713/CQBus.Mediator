using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.Executors;

public interface IStreamExecutor
{
    IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>;
}

internal sealed class StreamExecutor(IServiceProvider serviceProvider) : IStreamExecutor
{
    public async IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
    {
        IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> enumerable =
            serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();

        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors = enumerable switch
        {
            IStreamPipelineBehavior<TRequest, TResponse>[] arr => arr,
            _ => enumerable.ToArray()
        };

        IStreamRequestHandler<TRequest, TResponse> handler =
            serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

        IAsyncEnumerable<TResponse> asyncEnumerable = behaviors.Length == 0
            ? handler.Handle(request, cancellationToken)
            : new Chain<TRequest, TResponse>(behaviors, handler, request).Start(cancellationToken);

        await foreach (TResponse response in asyncEnumerable.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            yield return response;
        }
    }

    private sealed class Chain<TRequest, TResponse> where TRequest : IStreamRequest<TResponse>
    {
        private int _index;
        private readonly IStreamPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly IStreamRequestHandler<TRequest, TResponse> _handler;
        private readonly TRequest _request;
        private readonly StreamHandlerDelegate<TResponse> _next;

        internal Chain(
            IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
            IStreamRequestHandler<TRequest, TResponse> handler,
            TRequest request)
        {
            _behaviors = behaviors;
            _handler = handler;
            _request = request;
            _next = Next;
        }

        public IAsyncEnumerable<TResponse> Start(CancellationToken ct) =>
            _behaviors[_index++].Handle(_request, _next, ct);

        private IAsyncEnumerable<TResponse> Next(CancellationToken ct) =>
            _index >= _behaviors.Length
                ? _handler.Handle(_request, ct)
                : _behaviors[_index++].Handle(_request, _next, ct);
    }
}

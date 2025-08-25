using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

internal sealed class StreamPipelineBuilder
{
    public static StreamPipelineBuilder Instance { get; } = new();

    public async IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
    {
        var enumerable = (IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>>?)
            services.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>>));

        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors =
            enumerable as IStreamPipelineBehavior<TRequest, TResponse>[] ??
            (enumerable as List<IStreamPipelineBehavior<TRequest, TResponse>>)?.ToArray() ??
            [];

        IStreamRequestHandler<TRequest, TResponse> handler = services.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

        if (behaviors.Length == 0)
        {
            await foreach (TResponse item in handler.Handle(request, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        var chain = new Chain<TRequest, TResponse>(behaviors, handler, request);
        await foreach (TResponse item in chain.Next(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private sealed class Chain<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private int _index;
        private readonly IStreamPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly IStreamRequestHandler<TRequest, TResponse> _handler;
        private readonly TRequest _request;

        internal Chain(
            IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
            IStreamRequestHandler<TRequest, TResponse> handler,
            TRequest request)
        {
            _behaviors = behaviors;
            _handler = handler;
            _request = request;
        }

        public async IAsyncEnumerable<TResponse> Next([EnumeratorCancellation] CancellationToken ct)
        {
            if (_index >= _behaviors.Length)
            {
                await foreach (TResponse item in _handler.Handle(_request, ct).ConfigureAwait(false))
                {
                    yield return item;
                }

                yield break;
            }

            IStreamPipelineBehavior<TRequest, TResponse> current = _behaviors[_index++];
            await foreach (TResponse item in current.Handle(_request, Next, ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }
}

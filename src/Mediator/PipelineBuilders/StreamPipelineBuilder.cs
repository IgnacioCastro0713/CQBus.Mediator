using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface IStreamPipelineBuilder
{
    IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>;
}

internal sealed class StreamPipelineBuilder(IServiceProvider serviceProvider) : IStreamPipelineBuilder
{

    public async IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
    {
        IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> enumerable = serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors = enumerable switch
        {
            IStreamPipelineBehavior<TRequest, TResponse>[] arr => arr,
            List<IStreamPipelineBehavior<TRequest, TResponse>> list => list.ToArray(),
            _ => enumerable.ToArray()
        };

        IStreamRequestHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

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

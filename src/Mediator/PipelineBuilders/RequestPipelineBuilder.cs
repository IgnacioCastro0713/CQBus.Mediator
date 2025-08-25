using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface IRequestPipelineBuilder
{
    ValueTask<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>;
}

internal sealed class RequestPipelineBuilder(IServiceProvider serviceProvider) : IRequestPipelineBuilder
{
    public ValueTask<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> enumerable = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        IPipelineBehavior<TRequest, TResponse>[] behaviors = enumerable switch
        {
            IPipelineBehavior<TRequest, TResponse>[] arr => arr,
            List<IPipelineBehavior<TRequest, TResponse>> list => list.ToArray(),
            _ => enumerable.ToArray()
        };

        IRequestHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        if (behaviors.Length == 0)
        {
            return handler.Handle(request, cancellationToken);
        }

        var chain = new Chain<TRequest, TResponse>(behaviors, handler, request);
        return chain.Next(cancellationToken);
    }

    private sealed class Chain<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private int _index;
        private readonly IPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly IRequestHandler<TRequest, TResponse> _handler;
        private readonly TRequest _request;

        internal Chain(
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            IRequestHandler<TRequest, TResponse> handler,
            TRequest request)
        {
            _behaviors = behaviors;
            _handler = handler;
            _request = request;
        }

        public ValueTask<TResponse> Next(CancellationToken ct)
        {
            if (_index >= _behaviors.Length)
            {
                return _handler.Handle(_request, ct);
            }

            IPipelineBehavior<TRequest, TResponse> current = _behaviors[_index++];
            return current.Handle(_request, Next, ct);
        }
    }
}

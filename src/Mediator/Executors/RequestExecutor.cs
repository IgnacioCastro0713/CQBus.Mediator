using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.Executors;

public interface IRequestExecutor
{
    ValueTask<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>;
}

internal sealed class RequestExecutor(IServiceProvider serviceProvider) : IRequestExecutor
{
    public ValueTask<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> enumerable =
            serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        IPipelineBehavior<TRequest, TResponse>[] behaviors = enumerable switch
        {
            IPipelineBehavior<TRequest, TResponse>[] arr => arr,
            _ => enumerable.ToArray()
        };

        IRequestHandler<TRequest, TResponse> handler =
            serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        return behaviors.Length == 0
            ? handler.Handle(request, cancellationToken)
            : new Chain<TRequest, TResponse>(behaviors, handler, request).Start(cancellationToken);
    }

    private sealed class Chain<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private int _index;
        private readonly IPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly IRequestHandler<TRequest, TResponse> _handler;
        private readonly TRequest _request;
        private readonly RequestHandlerDelegate<TResponse> _next;

        internal Chain(
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            IRequestHandler<TRequest, TResponse> handler,
            TRequest request)
        {
            _behaviors = behaviors;
            _handler = handler;
            _request = request;
            _next = Next;
        }

        public ValueTask<TResponse> Start(CancellationToken ct) =>
            _behaviors[_index++].Handle(_request, _next, ct);

        private ValueTask<TResponse> Next(CancellationToken ct) => _index >= _behaviors.Length
            ? _handler.Handle(_request, ct)
            : _behaviors[_index++].Handle(_request, _next, ct);
    }
}

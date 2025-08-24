using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

internal sealed class RequestPipelineBuilder
{
    public static RequestPipelineBuilder Instance { get; } = new();

    public ValueTask<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        IPipelineBehavior<TRequest, TResponse>[] behaviors = Unsafe.As<IPipelineBehavior<TRequest, TResponse>[]>(services.GetServices<IPipelineBehavior<TRequest, TResponse>>());
        IRequestHandler<TRequest, TResponse> handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        if (behaviors.Length == 0)
        {
            return handler.Handle(request, cancellationToken);
        }

        RequestHandlerDelegate<TResponse> next = ct => handler.Handle(request, ct);
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> currentBehavior = behaviors[i];
            RequestHandlerDelegate<TResponse> current = next;
            next = ct => currentBehavior.Handle(request, current, ct);
        }

        return next(cancellationToken);
    }
}

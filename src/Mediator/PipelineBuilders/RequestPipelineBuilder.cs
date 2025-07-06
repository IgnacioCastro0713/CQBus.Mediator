using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface IRequestPipelineBuilder
{
    ValueTask<TResponse> BuildAndExecute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>;
}

internal sealed class RequestPipelineBuilder : IRequestPipelineBuilder
{
    public static RequestPipelineBuilder Instance { get; } = new();

    public ValueTask<TResponse> BuildAndExecute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        IRequestHandler<TRequest, TResponse> handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        IPipelineBehavior<TRequest, TResponse>[] behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(request, ct);

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> currentBehavior = behaviors[i];
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = ct => currentBehavior.Handle(request, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

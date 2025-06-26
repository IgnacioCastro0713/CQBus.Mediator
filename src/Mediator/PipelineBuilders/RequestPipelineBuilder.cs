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

internal class RequestPipelineBuilder : IRequestPipelineBuilder
{
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
            IPipelineBehavior<TRequest, TResponse> behavior = behaviors[i];
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = ct => behavior.Handle(request, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

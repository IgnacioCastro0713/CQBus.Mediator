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
    public ValueTask<TResponse> BuildAndExecute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        IRequestHandler<TRequest, TResponse> handler =
            services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviorsEnumerable =
            services.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        using IEnumerator<IPipelineBehavior<TRequest, TResponse>> behaviorEnumerator =
            behaviorsEnumerable.GetEnumerator();

        if (!behaviorEnumerator.MoveNext())
        {
            return handler.Handle(request, cancellationToken);
        }

        List<IPipelineBehavior<TRequest, TResponse>> behaviorsList =
        [
            behaviorEnumerator.Current
        ];

        while (behaviorEnumerator.MoveNext())
        {
            behaviorsList.Add(behaviorEnumerator.Current);
        }

        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(request, ct);

        for (int i = behaviorsList.Count - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> currentBehavior = behaviorsList[i];
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = ct => currentBehavior.Handle(request, next, ct);
        }

        return pipeline(cancellationToken);
    }
}

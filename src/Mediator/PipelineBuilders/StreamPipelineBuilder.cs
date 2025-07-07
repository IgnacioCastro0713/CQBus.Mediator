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
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>;
}

internal sealed class StreamPipelineBuilder : IStreamPipelineBuilder
{
    public static StreamPipelineBuilder Instance { get; } = new();

    public async IAsyncEnumerable<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
    {
        IStreamRequestHandler<TRequest, TResponse> handler = services.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors = services.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
        StreamHandlerDelegate<TResponse> pipeline = ct => handler.Handle(request, ct);

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IStreamPipelineBehavior<TRequest, TResponse> currentBehavior = behaviors[i];
            StreamHandlerDelegate<TResponse> next = pipeline;
            pipeline = ct => currentBehavior.Handle(request, _ => next(ct), ct);
        }

        await foreach (TResponse item in pipeline(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}

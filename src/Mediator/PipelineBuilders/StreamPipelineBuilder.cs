using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.PipelineBuilders;

public interface IStreamPipelineBuilder
{
    IAsyncEnumerable<TResponse> BuildAndExecute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>;
}

internal sealed class StreamPipelineBuilder : IStreamPipelineBuilder
{
    public async IAsyncEnumerable<TResponse> BuildAndExecute<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IStreamRequest<TResponse>
    {
        IStreamRequestHandler<TRequest, TResponse> handler =
            services.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
        IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviorsEnumerable =
            services.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();

        using IEnumerator<IStreamPipelineBehavior<TRequest, TResponse>> behaviorEnumerator =
            behaviorsEnumerable.GetEnumerator();

        if (!behaviorEnumerator.MoveNext())
        {
            await foreach (TResponse item in handler.Handle(request, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        List<IStreamPipelineBehavior<TRequest, TResponse>> behaviorList =
        [
            behaviorEnumerator.Current
        ];

        while (behaviorEnumerator.MoveNext())
        {
            behaviorList.Add(behaviorEnumerator.Current);
        }

        StreamHandlerDelegate<TResponse> pipeline = ct => handler.Handle(request, ct);

        for (int i = behaviorList.Count - 1; i >= 0; i--)
        {
            IStreamPipelineBehavior<TRequest, TResponse> currentBehavior = behaviorList[i];
            StreamHandlerDelegate<TResponse> next = pipeline;
            pipeline = ct => currentBehavior.Handle(request, _ => next(ct), ct);
        }

        await foreach (TResponse item in pipeline(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}

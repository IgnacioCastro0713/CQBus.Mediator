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
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors = Unsafe.As<IStreamPipelineBehavior<TRequest, TResponse>[]>(services.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>());
        IStreamRequestHandler<TRequest, TResponse> handler = services.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

        StreamHandlerDelegate<TResponse> next = ct => handler.Handle(request, ct);
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IStreamPipelineBehavior<TRequest, TResponse> currentBehavior = behaviors[i];
            StreamHandlerDelegate<TResponse> current = next;
            next = ct => currentBehavior.Handle(request, current, ct);
        }

        await foreach (TResponse item in next(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}

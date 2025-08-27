using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CQBus.Mediator.Executors;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests.Executors;

public sealed class StreamExecutorTests
{
    [ExcludeFromCodeCoverage]
    private sealed record Range(int Count) : IStreamRequest<int>;

    private sealed class RangeHandler : IStreamRequestHandler<Range, int>
    {
        public async IAsyncEnumerable<int> Handle(Range request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
                await Task.Yield();
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class B1 : IStreamPipelineBehavior<Range, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Range _,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next(cancellationToken).ConfigureAwait(false))
            {
                yield return item + 1;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class B2 : IStreamPipelineBehavior<Range, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Range _,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next(cancellationToken).ConfigureAwait(false))
            {
                yield return item * 10;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class ShortCircuit : IStreamPipelineBehavior<Range, int>
    {
        private readonly int _value;

        public ShortCircuit(int value) => _value = value;

        public async IAsyncEnumerable<int> Handle(
            Range _,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _value;
                await Task.Yield();
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class SlowRangeHandler : IStreamRequestHandler<Range, int>
    {
        public async IAsyncEnumerable<int> Handle(Range request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private static StreamExecutor Build(IServiceProvider sp)
    {
        return new StreamExecutor(sp);
    }


    [Fact]
    public async Task NoBehaviors_Passes_Through_Handler_Stream()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, RangeHandler>();

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        var collected = new List<int>();
        await foreach (int item in builder.Execute<Range, int>(new Range(3), CancellationToken.None)
                           .ConfigureAwait(false))
        {
            collected.Add(item);
        }

        Assert.Equal(new[] { 0, 1, 2 }, collected);
    }

    [Fact]
    public async Task TwoBehaviors_Run_In_Registration_Order()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, RangeHandler>();
        services.AddSingleton<IStreamPipelineBehavior<Range, int>, B1>();
        services.AddSingleton<IStreamPipelineBehavior<Range, int>, B2>();

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        var collected = new List<int>();
        await foreach (int item in builder.Execute<Range, int>(new Range(3), CancellationToken.None)
                           .ConfigureAwait(false))
        {
            collected.Add(item);
        }

        Assert.Equal(new[] { 1, 11, 21 }, collected);
    }

    [Fact]
    public async Task Order_Flipped_Produces_Different_Result()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, RangeHandler>();
        services.AddSingleton<IStreamPipelineBehavior<Range, int>, B2>();
        services.AddSingleton<IStreamPipelineBehavior<Range, int>, B1>();

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        List<int> collected = [];
        await foreach (int item in builder.Execute<Range, int>(new Range(3), CancellationToken.None)
                           .ConfigureAwait(false))
        {
            collected.Add(item);
        }

        Assert.Equal(new[] { 10, 20, 30 }, collected);
    }

    [Fact]
    public async Task ShortCircuit_Behavior_Skips_Handler_And_Other_Behaviors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, RangeHandler>();
        services.AddSingleton<IStreamPipelineBehavior<Range, int>>(new ShortCircuit(7));
        services.AddSingleton<IStreamPipelineBehavior<Range, int>, B1>();
        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        List<int> collected = [];
        await foreach (int item in builder.Execute<Range, int>(new Range(5), CancellationToken.None)
                           .ConfigureAwait(false))
        {
            collected.Add(item);
        }

        Assert.Equal(new[] { 7, 7, 7 }, collected);
    }

    [Fact]
    public async Task Cancellation_Before_Enumeration_Throws_Immediately()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, SlowRangeHandler>();

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        IAsyncEnumerator<int> enumerator =
            builder.Execute<Range, int>(new Range(100), cts.Token).GetAsyncEnumerator(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            try
            {
                await enumerator.MoveNextAsync();
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        });
    }

    [Fact]
    public async Task Cancellation_During_Enumeration_Is_Observed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<Range, int>, SlowRangeHandler>();

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        StreamExecutor builder = Build(sp);

        using var cts = new CancellationTokenSource();

        IAsyncEnumerator<int> enumerator =
            builder.Execute<Range, int>(new Range(100), cts.Token).GetAsyncEnumerator(cts.Token);

        try
        {
            Assert.True(await enumerator.MoveNextAsync());

            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await enumerator.MoveNextAsync();
            });
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    [Fact]
    public async Task Scoped_Resolution_Uses_Scope_Instances()
    {
        var services = new ServiceCollection();
        services.AddScoped<IStreamRequestHandler<Range, int>, RangeHandler>();
        services.AddScoped<IStreamPipelineBehavior<Range, int>, B1>();
        services.AddScoped<IStreamPipelineBehavior<Range, int>, B2>();

        ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = root.CreateScope();

        StreamExecutor builder = Build(scope.ServiceProvider);

        var collected = new List<int>();
        await foreach (int item in builder.Execute<Range, int>(new Range(3), CancellationToken.None)
                           .ConfigureAwait(false))
        {
            collected.Add(item);
        }

        Assert.Equal(new[] { 1, 11, 21 }, collected);
    }
}

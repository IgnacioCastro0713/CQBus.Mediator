using System.Diagnostics.CodeAnalysis;
using CQBus.Mediator.Executors;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests.Executors;


public class RequestExecutorTests
{
    [ExcludeFromCodeCoverage]
    private sealed record Echo(string Text) : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private sealed class EchoHandler : IRequestHandler<Echo, string>
    {
        public int Calls;
        public ValueTask<string> Handle(Echo request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return ValueTask.FromResult($"H({request.Text})");
        }
    }

    [ExcludeFromCodeCoverage]

    private sealed class B1 : IPipelineBehavior<Echo, string>
    {

        public async ValueTask<string> Handle(Echo request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            string inner = await next(cancellationToken);
            return $"B1>{inner}<B1";
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class B2 : IPipelineBehavior<Echo, string>
    {
        public async ValueTask<string> Handle(Echo request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            string inner = await next(cancellationToken);
            return $"B2>{inner}<B2";
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class ShortCircuit : IPipelineBehavior<Echo, string>
    {
        private readonly string _value;
        public ShortCircuit(string value) => _value = value;

        public ValueTask<string> Handle(
            Echo request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
            => ValueTask.FromResult($"SC({_value})");
    }

    [ExcludeFromCodeCoverage]
    private sealed class CancelAwareHandler : IRequestHandler<Echo, string>
    {
        public ValueTask<string> Handle(Echo request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.Text);
        }
    }

    [ExcludeFromCodeCoverage]
    private static RequestExecutor Build(IServiceCollection sc)
    {
        ServiceProvider sp = sc.BuildServiceProvider(validateScopes: true);
        return new RequestExecutor(sp);
    }

    [Fact]
    public async Task NoBehaviors_Calls_Handler_Directly()
    {
        var handler = new EchoHandler();

        var sc = new ServiceCollection();
        sc.AddSingleton<IRequestHandler<Echo, string>>(handler);

        RequestExecutor builder = Build(sc);

        string result = await builder.Execute<Echo, string>(new Echo("x"), CancellationToken.None);

        Assert.Equal("H(x)", result);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TwoBehaviors_Run_In_Registration_Order()
    {
        var handler = new EchoHandler();

        var sc = new ServiceCollection();
        sc.AddSingleton<IRequestHandler<Echo, string>>(handler);

        sc.AddSingleton<IPipelineBehavior<Echo, string>, B1>();
        sc.AddSingleton<IPipelineBehavior<Echo, string>, B2>();

        RequestExecutor builder = Build(sc);

        string result = await builder.Execute<Echo, string>(new Echo("x"), CancellationToken.None);

        Assert.Equal("B1>B2>H(x)<B2<B1", result);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Behavior_ShortCircuits_Handler_Is_Not_Called()
    {
        var handler = new EchoHandler();

        var sc = new ServiceCollection();
        sc.AddSingleton<IRequestHandler<Echo, string>>(handler);

        sc.AddSingleton<IPipelineBehavior<Echo, string>>(new ShortCircuit("stop"));
        sc.AddSingleton<IPipelineBehavior<Echo, string>, B1>();

        RequestExecutor builder = Build(sc);

        string result = await builder.Execute<Echo, string>(new Echo("x"), CancellationToken.None);

        Assert.Equal("SC(stop)", result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_Propagates_To_Handler()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IRequestHandler<Echo, string>, CancelAwareHandler>();

        RequestExecutor builder = Build(sc);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => _ = await builder.Execute<Echo, string>(new Echo("y"), cts.Token));
    }

    [Fact]
    public async Task Works_With_Single_Behavior()
    {
        var handler = new EchoHandler();

        var sc = new ServiceCollection();
        sc.AddSingleton<IRequestHandler<Echo, string>>(handler);
        sc.AddSingleton<IPipelineBehavior<Echo, string>, B2>();

        RequestExecutor builder = Build(sc);

        string result = await builder.Execute<Echo, string>(new Echo("z"), CancellationToken.None);

        Assert.Equal("B2>H(z)<B2", result);
        Assert.Equal(1, handler.Calls);
    }
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using CQBus.Mediator;
using Mediator.Benchmarks.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Benchmarks
{
    private IMediator _mediator;
    private MediatR.IMediator _mediatR;

    private IMediator _mediatorWithBehaviors;
    private MediatR.IMediator _mediatRWithBehaviors;

    private readonly TestRequest _request = new() { Message = "Hello World" };
    private readonly TestStreamRequest _streamRequest = new() { Message = "Hello World" };
    private readonly MediatRTestRequest _mediatRRequest = new() { Message = "Hello World" };
    private readonly MediatRTestStreamRequest _mediatRStreamRequest = new() { Message = "Hello World" };
    private readonly TestNotification _notification = new();
    private readonly MediatRTestNotification _mediatRNotification = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup without pipeline behaviors
        var services = new ServiceCollection();
        services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly));
        ServiceProvider provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
        _mediatR = provider.GetRequiredService<MediatR.IMediator>();

        // Setup with pipeline behaviors
        var servicesWithBehaviors = new ServiceCollection();
        servicesWithBehaviors.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamPipelineBehavior<,>));
        });
        servicesWithBehaviors.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingPipelineBehaviorR<,>));
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamPipelineBehaviorR<,>));
        });
        ServiceProvider providerWithBehaviors = servicesWithBehaviors.BuildServiceProvider();
        _mediatorWithBehaviors = providerWithBehaviors.GetRequiredService<IMediator>();
        _mediatRWithBehaviors = providerWithBehaviors.GetRequiredService<MediatR.IMediator>();
    }

    // Setup without pipeline behaviors

    [BenchmarkCategory("Send"), Benchmark(Baseline = true)]
    public Task<string> MediatR___Send() => _mediatR.Send(_mediatRRequest);

    [BenchmarkCategory("Send"), Benchmark]
    public ValueTask<string> Mediator__Send() => _mediator.Send(_request);

    [BenchmarkCategory("Publish"), Benchmark(Baseline = true)]
    public Task MediatR___Publish() => _mediatR.Publish(_mediatRNotification);

    [BenchmarkCategory("Publish"), Benchmark]
    public ValueTask Mediator__Publish() => _mediator.Publish(_notification);

    [BenchmarkCategory("CreateStream"), Benchmark(Baseline = true)]
    public IAsyncEnumerable<string> MediatR___CreateStream() => _mediatR.CreateStream(_mediatRStreamRequest);

    [BenchmarkCategory("CreateStream"), Benchmark]
    public IAsyncEnumerable<string> Mediator__CreateStream() => _mediator.CreateStream(_streamRequest);

    // Setup with pipeline behaviors

    //[BenchmarkCategory("Send"), Benchmark]
    //public Task<string> MediatR___Send_WithBehaviors() => _mediatRWithBehaviors.Send(_mediatRRequest);

    //[BenchmarkCategory("Send"), Benchmark]
    //public ValueTask<string> Mediator__Send_WithBehaviors() => _mediatorWithBehaviors.Send(_request);

    //[BenchmarkCategory("Publish"), Benchmark]
    //public Task MediatR___Publish_WithBehaviors() => _mediatRWithBehaviors.Publish(_mediatRNotification);

    //[BenchmarkCategory("Publish"), Benchmark]
    //public ValueTask Mediator__Publish_WithBehaviors() => _mediatorWithBehaviors.Publish(_notification);

    //[BenchmarkCategory("CreateStream"), Benchmark]
    //public IAsyncEnumerable<string> MediatR___CreateStream_WithBehaviors() => _mediatRWithBehaviors.CreateStream(_mediatRStreamRequest);

    //[BenchmarkCategory("CreateStream"), Benchmark]
    //public IAsyncEnumerable<string> Mediator__CreateStream_WithBehaviors() => _mediatorWithBehaviors.CreateStream(_streamRequest);

}

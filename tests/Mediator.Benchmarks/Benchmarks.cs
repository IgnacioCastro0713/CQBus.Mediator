using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using CQBus.Mediator;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
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

    private readonly TestRequest _request = new() { Message = "Hello World" };
    private readonly TestStreamRequest _streamRequest = new() { Message = "Hello World" };
    private readonly MediatRTestRequest _mediatRRequest = new() { Message = "Hello World" };
    private readonly MediatRTestStreamRequest _mediatRStreamRequest = new() { Message = "Hello World" };
    private readonly TestNotification _notification = new();
    private readonly MediatRTestNotification _mediatRNotification = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TextWriter.Null);
        services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly));
        ServiceProvider provider = services.BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
        _mediatR = provider.GetRequiredService<MediatR.IMediator>();
    }

    // --------------------------
    // Warm Benchmarks
    // --------------------------

    [BenchmarkCategory("Send"), Benchmark(Baseline = true)]
    public Task<string> MediatR___Send_Warm()
    {
        return _mediatR.Send(_mediatRRequest);
    }

    [BenchmarkCategory("Send"), Benchmark]
    public ValueTask<string> Mediator__Send_Warm()
    {
        return _mediator.Send(_request);
    }

    [BenchmarkCategory("CreateStream"), Benchmark(Baseline = true)]
    public IAsyncEnumerable<string> MediatR___CreateStream_Warm()
    {
        return _mediatR.CreateStream(_mediatRStreamRequest);
    }

    [BenchmarkCategory("CreateStream"), Benchmark]
    public IAsyncEnumerable<string> Mediator__CreateStream_Warm()
    {
        return _mediator.CreateStream(_streamRequest);
    }

    [BenchmarkCategory("Publish"), Benchmark(Baseline = true)]
    public Task MediatR___Publish_Warm()
    {
        return _mediatR.Publish(_mediatRNotification);
    }

    [BenchmarkCategory("Publish"), Benchmark]
    public ValueTask Mediator__Publish_Warm()
    {
        return _mediator.Publish(_notification);
    }
}

public class TestRequest : IRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class TestRequestHandler : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(string.Empty);
    }
}

public class TestStreamRequest : IStreamRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, string>
{
    public IAsyncEnumerable<string> Handle(TestStreamRequest request, CancellationToken cancellationToken)
    {
        return AsyncEnumerable.Empty<string>();
    }
}

public class MediatRTestRequest : MediatR.IRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class MediatRTestRequestHandler : MediatR.IRequestHandler<MediatRTestRequest, string>
{
    public Task<string> Handle(MediatRTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(string.Empty);
    }
}

public class MediatRTestStreamRequest : MediatR.IStreamRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class MediatRTestStreamRequestHandler : MediatR.IStreamRequestHandler<MediatRTestStreamRequest, string>
{
    public IAsyncEnumerable<string> Handle(MediatRTestStreamRequest request, CancellationToken cancellationToken)
    {
        return AsyncEnumerable.Empty<string>();
    }
}

public class TestNotification : INotification;

public class MediatRTestNotification : MediatR.INotification;

public class TestNotificationHandler : INotificationHandler<TestNotification>
{
    public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}

public class MediatRTestNotificationHandler : MediatR.INotificationHandler<MediatRTestNotification>
{
    public Task Handle(MediatRTestNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;

namespace Mediator.Benchmarks.Requests;

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

public class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);
        return response;
    }
}

public class LoggingStreamPipelineBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (TResponse response in next(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return response;
        }
    }
}


public class LoggingPipelineBehaviorR<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        return response;
    }
}

public class LoggingStreamPipelineBehaviorR<TRequest, TResponse> : MediatR.IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        MediatR.StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (TResponse response in next().WithCancellation(cancellationToken))
        {
            yield return response;
        }
    }
}

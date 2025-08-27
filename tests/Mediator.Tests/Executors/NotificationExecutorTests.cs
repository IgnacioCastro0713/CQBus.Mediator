using System.Diagnostics.CodeAnalysis;
using CQBus.Mediator.Executors;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests.Executors;

public sealed class NotificationExecutorTests
{
    [ExcludeFromCodeCoverage]
    private sealed record Notice(string Text) : INotification;

    [ExcludeFromCodeCoverage]
    private sealed class HandlerA : INotificationHandler<Notice>
    {
        public int Calls { get; private set; }

        public ValueTask Handle(Notice notification, CancellationToken cancellationToken)
        {
            Calls += 1;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class HandlerB : INotificationHandler<Notice>
    {
        public int Calls { get; private set; }

        public ValueTask Handle(Notice notification, CancellationToken cancellationToken)
        {
            Calls += 1;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class CapturingPublisher : INotificationPublisher
    {
        public object? LastNotification { get; private set; }
        public Array? LastHandlersArray { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public int Calls { get; private set; }

        public ValueTask Publish<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            Calls += 1;
            LastNotification = notification!;
            LastHandlersArray = handlers;
            LastToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromCodeCoverage]
    private static NotificationExecutor Build(IServiceProvider serviceProvider)
    {
        return new NotificationExecutor(serviceProvider);
    }


    [Fact]
    public async Task NoHandlers_Passes_EmptyArray_To_Publisher()
    {
        var services = new ServiceCollection();

        var publisher = new CapturingPublisher();
        services.AddSingleton<INotificationPublisher>(publisher);

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        NotificationExecutor executor = Build(sp);

        var notification = new Notice("x");

        await executor.Execute(notification, publisher, CancellationToken.None);

        Assert.Equal(1, publisher.Calls);
        Assert.Same(notification, publisher.LastNotification);
        Assert.NotNull(publisher.LastHandlersArray);
        Assert.Empty(publisher.LastHandlersArray);
        Assert.False(publisher.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task MultipleHandlers_Are_Passed_In_Registration_Order()
    {
        var services = new ServiceCollection();

        var a = new HandlerA();
        var b = new HandlerB();
        var publisher = new CapturingPublisher();

        services.AddSingleton<INotificationHandler<Notice>>(a);
        services.AddSingleton<INotificationHandler<Notice>>(b);
        services.AddSingleton<INotificationPublisher>(publisher);

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        NotificationExecutor executor = Build(sp);

        var notification = new Notice("hello");

        await executor.Execute(notification, publisher, CancellationToken.None);

        Assert.Equal(1, publisher.Calls);
        Assert.NotNull(publisher.LastHandlersArray);
        Assert.Equal(2, publisher.LastHandlersArray!.Length);

        INotificationHandler<Notice>[] typed = publisher.LastHandlersArray.Cast<INotificationHandler<Notice>>()
            .ToArray();
        Assert.IsType<INotificationHandler<Notice>[]>(typed);
        Assert.Same(a, typed[0]);
        Assert.Same(b, typed[1]);
    }

    [Fact]
    public async Task CancellationToken_Is_Forwarded_To_Publisher()
    {
        var services = new ServiceCollection();

        var publisher = new CapturingPublisher();
        services.AddSingleton<INotificationPublisher>(publisher);

        ServiceProvider sp = services.BuildServiceProvider(validateScopes: true);
        NotificationExecutor executor = Build(sp);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await executor.Execute(new Notice("y"), publisher, cts.Token);

        Assert.True(publisher.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ScopedHandlers_Are_Resolved_From_Scope()
    {
        var services = new ServiceCollection();

        var publisher = new CapturingPublisher();
        services.AddScoped<INotificationHandler<Notice>, HandlerA>();
        services.AddScoped<INotificationHandler<Notice>, HandlerB>();
        services.AddSingleton<INotificationPublisher>(publisher);

        ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = root.CreateScope();

        NotificationExecutor executor = Build(scope.ServiceProvider);

        var notification = new Notice("z");

        await executor.Execute(notification, publisher, CancellationToken.None);

        Assert.Equal(2, publisher.LastHandlersArray!.Length);

        INotificationHandler<Notice>[] typed = publisher.LastHandlersArray!.Cast<INotificationHandler<Notice>>()
            .ToArray();
        Assert.IsType<HandlerA>(typed[0]);
        Assert.IsType<HandlerB>(typed[1]);
        Assert.NotSame(typed[0], typed[1]);
    }
}

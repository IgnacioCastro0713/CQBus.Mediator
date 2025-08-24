using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using CQBus.Mediator;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Maps;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Moq;

namespace Mediator.Tests;

public class MediatorWithMapsTests
{
    public sealed class TestRequest : IRequest<string>
    {
        public string Message { get; set; } = "";
    }

    public sealed class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
            => ValueTask.FromResult($"Handled: {request.Message}");
    }

    public sealed class TestNotification : INotification
    {
        public string Message { get; set; } = "";
    }

    public sealed class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public ValueTask Handle(TestNotification notification, CancellationToken ct)
            => ValueTask.CompletedTask;
    }

    public sealed class TestStreamRequest : IStreamRequest<int>
    {
        public int Count { get; set; }
    }

    public sealed class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(TestStreamRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 1; i <= request.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                yield return i;
                await Task.Yield();
            }
        }
    }

    // =========================
    //          SEND
    // =========================
    [Fact]
    public async Task Send_Returns_Response_When_Handler_Exists()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(new TestRequestHandler());
        sp.Setup(s => s.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<TestRequest, string>>());

        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Loose);
        var reqInvoker = (RequestInvoker<string>)MediatorInvoker.Request<TestRequest, string>;
        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>
        {
            { (typeof(TestRequest), typeof(string)), reqInvoker }
        }.ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        string result = await mediator.Send(new TestRequest { Message = "Hi" });

        Assert.Equal("Handled: Hi", result);

        sp.VerifyAll();
        maps.VerifyGet(m => m.Requests, Times.AtLeastOnce());
    }


    [Fact]
    public async Task Send_Throws_ArgumentNull_When_Request_Null()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Loose);
        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Loose);
        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Streams).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Send<string>(null!, CancellationToken.None));
    }

    // =========================
    //        PUBLISH
    // =========================
    [Fact]
    public async Task Publish_Completes_When_No_Handler_Registered()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);

        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Strict);
        maps.SetupGet(m => m.Notifications)
            .Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        await mediator.Publish(new TestNotification { Message = "noop" });

        maps.VerifyGet(m => m.Notifications, Times.AtLeastOnce());
        sp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Publish_Invokes_Handler_When_Registered()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new[] { new TestNotificationHandler() });

        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Strict);
        var notifInvoker = (NotificationInvoker<TestNotification>)MediatorInvoker.Notification<TestNotification>;

        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>
        {
            { typeof(TestNotification), notifInvoker }
        }.ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        await mediator.Publish(new TestNotification { Message = "hello" });

        sp.VerifyAll();
        maps.VerifyGet(m => m.Notifications, Times.AtLeastOnce());
    }

    [Fact]
    public async Task Publish_Throws_ArgumentNull_When_Notification_Null()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Loose);
        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Loose);
        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Streams).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Publish<TestNotification>(null!, CancellationToken.None));
    }

    // =========================
    //         STREAM
    // =========================
    [Fact]
    public async Task CreateStream_Yields_Sequence_When_Handler_Exists()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);

        sp.Setup(s => s.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(new TestStreamRequestHandler());

        sp.Setup(s => s.GetService(
                typeof(IEnumerable<CQBus.Mediator.Pipelines.IStreamPipelineBehavior<TestStreamRequest, int>>)))
            .Returns(Array.Empty<CQBus.Mediator.Pipelines.IStreamPipelineBehavior<TestStreamRequest, int>>());

        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Strict);
        var streamInvoker = (StreamInvoker<int>)MediatorInvoker.Stream<TestStreamRequest, int>;
        maps.SetupGet(m => m.Streams).Returns(new Dictionary<(Type, Type), Delegate>
        {
            { (typeof(TestStreamRequest), typeof(int)), streamInvoker }
        }.ToFrozenDictionary());

        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        var req = new TestStreamRequest { Count = 3 };
        var list = new List<int>();
        await foreach (int i in mediator.CreateStream<int>(req))
        {
            list.Add(i);
        }

        Assert.Equal(new[] { 1, 2, 3 }, list);

        sp.VerifyAll();
        maps.VerifyGet(m => m.Streams, Times.AtLeastOnce());
    }


    [Fact]
    public void CreateStream_Throws_ArgumentNull_When_Request_Null()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Loose);
        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Loose);
        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Streams).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            mediator.CreateStream<int>(null!, CancellationToken.None));
        Assert.Equal("request", ex.ParamName);
    }

    // =========================
    //     CONSTRUCTOR
    // =========================
    [Fact]
    public void Ctor_Works_With_Maps()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Loose);
        var maps = new Mock<IMediatorDispatchMaps>(MockBehavior.Loose);
        maps.SetupGet(m => m.Requests).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Notifications).Returns(new Dictionary<Type, Delegate>().ToFrozenDictionary());
        maps.SetupGet(m => m.Streams).Returns(new Dictionary<(Type, Type), Delegate>().ToFrozenDictionary());

        var mediator = new CQBus.Mediator.Mediator(sp.Object, maps.Object);

        Assert.NotNull(mediator);
    }
}

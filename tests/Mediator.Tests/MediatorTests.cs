using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CQBus.Mediator.Executors;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Invokers;
using CQBus.Mediator.Maps;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Moq;

namespace Mediator.Tests;


public class MediatorTests
{
    [ExcludeFromCodeCoverage]
    private sealed record Ping(string Message) : IRequest<string>;
    [ExcludeFromCodeCoverage]
    private sealed record StreamPing(int Count) : IStreamRequest<int>;
    [ExcludeFromCodeCoverage]
    private sealed record Notice(string Text) : INotification;

    [ExcludeFromCodeCoverage]
    private sealed class TestMaps : IMediatorDispatchMaps
    {
        public TestMaps(
            FrozenDictionary<(Type, Type), Delegate> req,
            FrozenDictionary<Type, Delegate> noti,
            FrozenDictionary<(Type, Type), Delegate> str)
        {
            Requests = req;
            Notifications = noti;
            Streams = str;
        }

        public FrozenDictionary<(Type, Type), Delegate> Requests { get; }
        public FrozenDictionary<Type, Delegate> Notifications { get; }
        public FrozenDictionary<(Type, Type), Delegate> Streams { get; }
    }

    [ExcludeFromCodeCoverage]
    private static TestMaps BuildMaps(
        Action<Dictionary<(Type, Type), Delegate>>? addRequests = null,
        Action<Dictionary<Type, Delegate>>? addNotifications = null,
        Action<Dictionary<(Type, Type), Delegate>>? addStreams = null)
    {
        var req = new Dictionary<(Type, Type), Delegate>();
        var noti = new Dictionary<Type, Delegate>();
        var str = new Dictionary<(Type, Type), Delegate>();

        addRequests?.Invoke(req);
        addNotifications?.Invoke(noti);
        addStreams?.Invoke(str);

        return new TestMaps(
            req.ToFrozenDictionary(),
            noti.ToFrozenDictionary(),
            str.ToFrozenDictionary());
    }

    [ExcludeFromCodeCoverage]
    private static CQBus.Mediator.Mediator CreateMediator(
        IMediatorDispatchMaps maps,
        out Mock<IRequestExecutor> reqBuilder,
        out Mock<INotificationExecutor> notifBuilder,
        out Mock<IStreamExecutor> streamBuilder,
        out INotificationPublisher publisher)
    {
        reqBuilder = new Mock<IRequestExecutor>(MockBehavior.Strict);
        notifBuilder = new Mock<INotificationExecutor>(MockBehavior.Strict);
        streamBuilder = new Mock<IStreamExecutor>(MockBehavior.Strict);

        var factory = new Mock<IExecutorFactory>(MockBehavior.Strict);
        factory.SetupGet(f => f.Request).Returns(reqBuilder.Object);
        factory.SetupGet(f => f.Notification).Returns(notifBuilder.Object);
        factory.SetupGet(f => f.Stream).Returns(streamBuilder.Object);

        publisher = new TestPublisher();
        return new CQBus.Mediator.Mediator(factory.Object, maps, publisher);
    }

    [ExcludeFromCodeCoverage]
    private sealed class TestPublisher : INotificationPublisher
    {
        public List<object> Published { get; } = [];

        public ValueTask Publish<TNotification>(INotificationHandler<TNotification>[] handlers, TNotification notification, CancellationToken cancellationToken) where TNotification : INotification
        {
            Published.Add(notification!);
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromCodeCoverage]
    private static async IAsyncEnumerable<int> StreamRange(int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }

    // -----------------------------
    //              Send
    // -----------------------------

    [Fact]
    public async Task Send_Returns_Response_When_Map_Exists()
    {
        IMediatorDispatchMaps maps = BuildMaps(addRequests: req =>
        {
            req.Add((typeof(Ping), typeof(string)),
                (RequestInvoker<string>)((builder, request, ct) =>
                    new ValueTask<string>(((Ping)request).Message.ToUpperInvariant())));
        });

        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);
        string result = await mediator.Send(new Ping("hello"));
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public async Task Send_Throws_When_Map_Missing()
    {
        TestMaps maps = BuildMaps();
        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new Ping("x")).AsTask());
        Assert.Contains("No IRequest handler map", ex.Message);
    }

    // -----------------------------
    //            Publish
    // -----------------------------

    [Fact]
    public async Task Publish_Does_Nothing_When_No_Handlers()
    {
        TestMaps maps = BuildMaps();
        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out INotificationPublisher publisher);

        await mediator.Publish(new Notice("nada"));
        Assert.Empty(((TestPublisher)publisher).Published);
    }

    [Fact]
    public async Task Publish_Invokes_Handler_When_Map_Exists()
    {
        bool called = false;

        TestMaps maps = BuildMaps(addNotifications: noti =>
        {
            noti.Add(typeof(Notice),
                (NotificationInvoker<Notice>)((builder, n, pub, ct) =>
                {
                    called = true;
                    return ValueTask.CompletedTask;
                }));
        });

        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);
        await mediator.Publish(new Notice("hola"));

        Assert.True(called);
    }

    // -----------------------------
    //          CreateStream
    // -----------------------------

    [Fact]
    public async Task CreateStream_Returns_Values_When_Map_Exists()
    {
        TestMaps maps = BuildMaps(addStreams: str =>
        {
            str.Add((typeof(StreamPing), typeof(int)),
                (StreamInvoker<int>)((builder, request, ct) =>
                    StreamRange(((StreamPing)request).Count, ct)));
        });

        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);

        var list = new List<int>();
        await foreach (int v in mediator.CreateStream(new StreamPing(3)))
        {
            list.Add(v);
        }

        Assert.Equal(new[] { 0, 1, 2 }, list);
    }

    [Fact]
    public void CreateStream_Throws_When_Map_Missing()
    {
        TestMaps maps = BuildMaps();
        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            mediator.CreateStream(new StreamPing(1));
        });

        Assert.Contains("No IStream handler map", ex.Message);
    }

    [Fact]
    public async Task CreateStream_Respects_CancellationToken()
    {
        TestMaps maps = BuildMaps(addStreams: str =>
        {
            str.Add((typeof(StreamPing), typeof(int)),
                (StreamInvoker<int>)((builder, request, ct) =>
                    StreamRange(((StreamPing)request).Count, ct)));
        });

        CQBus.Mediator.Mediator mediator = CreateMediator(maps, out _, out _, out _, out _);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        IAsyncEnumerator<int> asyncEnum = mediator.CreateStream(new StreamPing(10), cts.Token)
            .GetAsyncEnumerator(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            try
            {
                await asyncEnum.MoveNextAsync();
            }
            finally
            {
                await asyncEnum.DisposeAsync();
            }
        });
    }

}

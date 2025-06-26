using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Moq;

namespace Mediator.Tests.NotificationPublishers;

public class ForeachAwaitPublisherTests
{
    // Test notification class
    public sealed record TestNotification(string Message) : INotification;

    [Fact]
    public async Task Publish_ShouldCallAllHandlers()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        var handler3Mock = new Mock<INotificationHandler<TestNotification>>();

        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        handler2Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        handler3Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object,
            handler3Mock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act
        await publisher.Publish(handlers, notification, CancellationToken.None);

        // Assert
        handler1Mock.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
        handler2Mock.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
        handler3Mock.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_ShouldHandleEmptyHandlersList()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var handlers = new List<INotificationHandler<TestNotification>>();
        var publisher = new ForeachAwaitPublisher();

        // Act & Assert (no exception should be thrown)
        await publisher.Publish(handlers, notification, CancellationToken.None);
    }

    [Fact]
    public async Task Publish_ShouldPassCancellationTokenToHandlers()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var handlerMock = new Mock<INotificationHandler<TestNotification>>();

        var cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        handlerMock.Setup(h => h.Handle(notification, token))
            .Returns(ValueTask.CompletedTask);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handlerMock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act
        await publisher.Publish(handlers, notification, token);

        // Assert
        handlerMock.Verify(h => h.Handle(notification, token), Times.Once);
    }

    [Fact]
    public async Task Publish_ShouldExecuteHandlersInOrder()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var executionOrder = new List<int>();

        // Create handler mocks that record their execution order
        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.Run(() => executionOrder.Add(1))));

        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        handler2Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.Run(() => executionOrder.Add(2))));

        var handler3Mock = new Mock<INotificationHandler<TestNotification>>();
        handler3Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.Run(() => executionOrder.Add(3))));

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object,
            handler3Mock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act
        await publisher.Publish(handlers, notification, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task Publish_ShouldWaitForHandlerToComplete_BeforeExecutingNext()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var executionTimes = new List<DateTime>();

        // Create a handler that takes some time to complete
        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executionTimes.Add(DateTime.Now); // First handler starts
                await Task.Delay(100); // Delay to simulate work
                executionTimes.Add(DateTime.Now); // First handler completes

            });

        // Create a second handler that executes quickly
        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        handler2Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executionTimes.Add(DateTime.Now); // Second handler execution
                await Task.Delay(100); // Delay to simulate work
            });


        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act
        await publisher.Publish(handlers, notification, CancellationToken.None);

        // Assert
        Assert.Equal(3, executionTimes.Count);


        // First handler completes
        DateTime firstHandlerComplete = executionTimes[1];

        // Second handler executes
        DateTime secondHandlerExecutes = executionTimes[2];

        // Verify second handler executed after first handler completed
        Assert.True(secondHandlerExecutes >= firstHandlerComplete);
    }

    [Fact]
    public async Task Publish_ShouldPropagateException_WhenHandlerThrows()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var exception = new InvalidOperationException("Handler error");

        var handlerMock = new Mock<INotificationHandler<TestNotification>>();
        handlerMock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Throws(exception);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handlerMock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act & Assert
        InvalidOperationException thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.Publish(handlers, notification, CancellationToken.None));

        Assert.Same(exception, thrownException);
    }

    [Fact]
    public async Task Publish_ShouldStopExecution_WhenCancellationRequested()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var cts = new CancellationTokenSource();

        // Track which handlers were executed
        var handlerExecuted = new List<int>();

        // First handler that will cancel the token
        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                handlerExecuted.Add(1);
                // Cancel the token immediately
                await cts.CancelAsync();
            });

        // Second handler that should check cancellation
        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        handler2Mock.Setup(h => h.Handle(notification, cts.Token))
            .Callback(() => cts.Token.ThrowIfCancellationRequested())
            .Returns(ValueTask.CompletedTask);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object
        };

        var publisher = new ForeachAwaitPublisher();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await publisher.Publish(handlers, notification, cts.Token));

        // Verify only first handler was executed
        Assert.Single(handlerExecuted);
        Assert.Equal(1, handlerExecuted[0]);
    }
}

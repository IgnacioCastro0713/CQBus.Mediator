using System.Collections.Concurrent;
using System.Diagnostics;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using Moq;

namespace Mediator.Tests.NotificationPublishers;

public class TaskWhenAllPublisherTests
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

        var publisher = new TaskWhenAllPublisher();

        // Act
        await publisher.Publish(handlers.ToArray(), notification, CancellationToken.None);

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
        var publisher = new TaskWhenAllPublisher();

        // Act & Assert (no exception should be thrown)
        await publisher.Publish(handlers.ToArray(), notification, CancellationToken.None);
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

        var publisher = new TaskWhenAllPublisher();

        // Act
        await publisher.Publish(handlers.ToArray(), notification, token);

        // Assert
        handlerMock.Verify(h => h.Handle(notification, token), Times.Once);
    }

    [Fact]
    public async Task Publish_ShouldPropagateException_WhenHandlerThrows()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var exception = new InvalidOperationException("Handler error");

        var handlerMock = new Mock<INotificationHandler<TestNotification>>();
        handlerMock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handlerMock.Object
        };

        var publisher = new TaskWhenAllPublisher();

        // Act & Assert
        InvalidOperationException thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.Publish(handlers.ToArray(), notification, CancellationToken.None));

        Assert.Same(exception, thrownException);
    }

    [Fact]
    public async Task Publish_ShouldPropagateFirstExceptionDirectly_WhenMultipleHandlersThrow()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var exception1 = new InvalidOperationException("Handler 1 error");
        var exception2 = new ArgumentException("Handler 2 error");

        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception1);

        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        handler2Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception2);

        var handler3Mock = new Mock<INotificationHandler<TestNotification>>();
        handler3Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object,
            handler3Mock.Object
        };

        var publisher = new TaskWhenAllPublisher();

        // Act & Assert
        // When awaiting Task.WhenAll, the first exception will be thrown directly
        Exception thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.Publish(handlers.ToArray(), notification, CancellationToken.None));

        // The exception could be either of our exceptions since they run concurrently
        Assert.True(thrownException == exception1 || thrownException == exception2);
    }

    [Fact]
    public async Task Publish_ShouldContainAllExceptions_InAggregateException_WhenAccessingTaskDirectly()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var exception1 = new InvalidOperationException("Handler 1 error");
        var exception2 = new ArgumentException("Handler 2 error");

        var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
        handler1Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception1);

        var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
        handler2Mock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception2);

        var handlers = new List<INotificationHandler<TestNotification>>
        {
            handler1Mock.Object,
            handler2Mock.Object
        };

        var publisher = new TaskWhenAllPublisher();

        // Act
        ValueTask publishTask = publisher.Publish(handlers.ToArray(), notification, CancellationToken.None);
        Task task = publishTask.AsTask();

        // Wait for the task to complete without blocking
        try
        {
            await task;
            Assert.Fail("Expected exceptions were not thrown");
        }
        catch (Exception)
        {
            // Exception expected, continue to check the task state
        }


        // Assert
        Assert.Equal(TaskStatus.Faulted, task.Status);
        Assert.NotNull(task.Exception);
        Assert.IsType<AggregateException>(task.Exception);

        AggregateException aggregateException = task.Exception;
        Assert.Equal(2, aggregateException.InnerExceptions.Count);
        Assert.Contains(exception1, aggregateException.InnerExceptions);
        Assert.Contains(exception2, aggregateException.InnerExceptions);
    }

    [Fact]
    public async Task Publish_ShouldExecuteHandlersConcurrently()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var executionStartTimes = new ConcurrentBag<DateTime>();
        var executionEndTimes = new ConcurrentBag<DateTime>();

        // Create handlers that will have delay but track their start/end times
        var handlers = new List<INotificationHandler<TestNotification>>();
        for (int i = 0; i < 3; i++)
        {
            // Use a local variable to avoid the closure issue
            int delay = 100 * (i + 1);

            var handlerMock = new Mock<INotificationHandler<TestNotification>>();
            handlerMock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
                .Returns(async (TestNotification n, CancellationToken ct) =>
                {
                    DateTime startTime = DateTime.Now;
                    executionStartTimes.Add(startTime);

                    await Task.Delay(delay, ct);

                    DateTime endTime = DateTime.Now;
                    executionEndTimes.Add(endTime);
                });

            handlers.Add(handlerMock.Object);
        }

        var publisher = new TaskWhenAllPublisher();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await publisher.Publish(handlers.ToArray(), notification, CancellationToken.None);
        stopwatch.Stop();

        // Assert

        // If handlers ran concurrently, total time should be close to the longest handler (~300ms)
        // If handlers ran sequentially, it would be about 600ms (100+200+300)
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Expected concurrent execution taking ~300ms, but took {stopwatch.ElapsedMilliseconds}ms");

        // All handlers should start at roughly the same time
        DateTime firstStart = executionStartTimes.Min();
        Assert.All(executionStartTimes, time =>
            Assert.True((time - firstStart).TotalMilliseconds < 50,
                $"Handler started too long after first handler: {(time - firstStart).TotalMilliseconds}ms"));

        // With concurrent execution, some handlers should complete before others even start
        Assert.Equal(3, executionStartTimes.Count);
        Assert.Equal(3, executionEndTimes.Count);
    }

    [Fact]
    public async Task Publish_ShouldRespectCancellationToken()
    {
        // Arrange
        var notification = new TestNotification("Test message");
        var cts = new CancellationTokenSource();
        var completedHandlers = new ConcurrentBag<int>();

        var handlers = new List<INotificationHandler<TestNotification>>();
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var handlerMock = new Mock<INotificationHandler<TestNotification>>();
            handlerMock.Setup(h => h.Handle(notification, It.IsAny<CancellationToken>()))
                .Returns(async (TestNotification _, CancellationToken ct) =>
                {
                    try
                    {
                        if (index == 0)
                        {
                            // First handler cancels after a brief delay
                            await Task.Delay(10, ct);
                            completedHandlers.Add(index);
                            cts.Cancel();
                            return;
                        }

                        // Other handlers take longer and should be cancelled
                        await Task.Delay(500, ct);
                        completedHandlers.Add(index); // This shouldn't execute if cancelled
                    }
                    catch (OperationCanceledException)
                    {
                        // Just propagate the cancellation
                        throw;
                    }
                });

            handlers.Add(handlerMock.Object);
        }

        var publisher = new TaskWhenAllPublisher();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await publisher.Publish(handlers.ToArray(), notification, cts.Token));

        // Only the first handler should have completed successfully
        Assert.Single(completedHandlers);
        Assert.Contains(0, completedHandlers);
    }
}

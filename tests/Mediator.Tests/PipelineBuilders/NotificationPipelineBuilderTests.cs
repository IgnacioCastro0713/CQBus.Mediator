using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Mediator.Tests.PipelineBuilders;

public class NotificationPipelineBuilderTests
{
    // Test notification class
    public record TestNotification(string Message) : INotification;

    // Test notification handler implementation
    public class TestNotificationHandler(List<string> receivedMessages) : INotificationHandler<TestNotification>
    {
        public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            receivedMessages.Add($"Handler received: {notification.Message}");
            return ValueTask.CompletedTask;
        }
    }

    // Second notification handler for testing multiple handlers
    public class SecondTestNotificationHandler(List<string> receivedMessages) : INotificationHandler<TestNotification>
    {
        public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            receivedMessages.Add($"Second handler received: {notification.Message}");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task BuildAndExecute_ShouldCallPublisherWithHandlers()
    {
        // Arrange
        var publisherMock = new Mock<INotificationPublisher>();
        var servicesMock = new Mock<IServiceProvider>();
        var notification = new TestNotification("Test message");
        var handlers = new List<INotificationHandler<TestNotification>>();

        servicesMock
            .Setup(s => s.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(handlers);

        publisherMock
            .Setup(p => p.Publish(It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                   It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute(notification, servicesMock.Object, publisherMock.Object, CancellationToken.None);

        // Assert
        publisherMock.Verify(
            p => p.Publish(
                It.Is<IEnumerable<INotificationHandler<TestNotification>>>(h => h == handlers),
                It.Is<TestNotification>(n => n == notification),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldResolveHandlersFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var receivedMessages = new List<string>();

        // Register handlers
        services.AddTransient<INotificationHandler<TestNotification>>(_ => new TestNotificationHandler(receivedMessages));
        services.AddTransient<INotificationHandler<TestNotification>>(_ => new SecondTestNotificationHandler(receivedMessages));

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        var publisherMock = new Mock<INotificationPublisher>();
        var notification = new TestNotification("Test notification");

        // Setup publisher to actually execute handlers
        publisherMock
            .Setup(p => p.Publish(It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                   It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<INotificationHandler<TestNotification>> handlers, TestNotification n, CancellationToken ct) =>
            {
                IEnumerable<Task> tasks = handlers.Select(h => h.Handle(n, ct).AsTask());
                return new ValueTask(Task.WhenAll(tasks));
            });

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute(notification, serviceProvider, publisherMock.Object, CancellationToken.None);

        // Assert
        Assert.Equal(2, receivedMessages.Count);
        Assert.Contains("Handler received: Test notification", receivedMessages);
        Assert.Contains("Second handler received: Test notification", receivedMessages);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldWorkWithNoHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        var publisherMock = new Mock<INotificationPublisher>();
        var notification = new TestNotification("No handlers");

        publisherMock
            .Setup(p => p.Publish(It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                   It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute(notification, serviceProvider, publisherMock.Object, CancellationToken.None);

        // Assert
        publisherMock.Verify(
            p => p.Publish(
                It.Is<IEnumerable<INotificationHandler<TestNotification>>>(h => !h.Any()),
                It.Is<TestNotification>(n => n == notification),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldRespectCancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        var receivedMessages = new List<string>();

        services.AddTransient<INotificationHandler<TestNotification>>(_ => new TestNotificationHandler(receivedMessages));

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        var publisherMock = new Mock<INotificationPublisher>();
        var notification = new TestNotification("Cancellation test");
        var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        publisherMock
            .Setup(p => p.Publish(It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                   It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.FromCanceled<object>(cts.Token)));

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await pipelineBuilder.BuildAndExecute(notification, serviceProvider, publisherMock.Object, cts.Token));

        publisherMock.Verify(
            p => p.Publish(
                It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                It.IsAny<TestNotification>(),
                It.Is<CancellationToken>(ct => ct.IsCancellationRequested)),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldWorkWithRealPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        var receivedMessages = new List<string>();

        // Register handlers
        services.AddTransient<INotificationHandler<TestNotification>>(_ => new TestNotificationHandler(receivedMessages));
        services.AddTransient<INotificationHandler<TestNotification>>(_ => new SecondTestNotificationHandler(receivedMessages));

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        var publisher = new ForeachAwaitPublisher(); // Real publisher implementation
        var notification = new TestNotification("Real publisher test");

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute(notification, serviceProvider, publisher, CancellationToken.None);

        // Assert
        Assert.Equal(2, receivedMessages.Count);
        Assert.Contains("Handler received: Real publisher test", receivedMessages);
        Assert.Contains("Second handler received: Real publisher test", receivedMessages);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldOrderHandlersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var executionOrder = new List<string>();

        // Create handlers that record execution order
        services.AddTransient<INotificationHandler<TestNotification>>(_ =>
            new OrderedNotificationHandler(executionOrder, "First", 100));
        services.AddTransient<INotificationHandler<TestNotification>>(_ =>
            new OrderedNotificationHandler(executionOrder, "Second", 50));
        services.AddTransient<INotificationHandler<TestNotification>>(_ =>
            new OrderedNotificationHandler(executionOrder, "Third", 0));

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        var publisher = new ForeachAwaitPublisher();
        var notification = new TestNotification("Order test");

        var pipelineBuilder = new NotificationPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute(notification, serviceProvider, publisher, CancellationToken.None);

        // Assert
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("First", executionOrder[0]);
        Assert.Equal("Second", executionOrder[1]);
        Assert.Equal("Third", executionOrder[2]);
    }

    // Helper class for testing execution order
    public sealed class OrderedNotificationHandler(List<string> executionOrder, string name, int delayMs)
        : INotificationHandler<TestNotification>
    {
        public async ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            executionOrder.Add(name);
        }
    }
}

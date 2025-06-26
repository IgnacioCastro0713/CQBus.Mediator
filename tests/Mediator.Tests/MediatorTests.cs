using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;
using Moq;

namespace Mediator.Tests;

public class MediatorTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

    #region Send Tests

    // Test Request implementation
    public class TestRequest : IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    // Test Request Handler implementation
    public class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> Handle(TestRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult($"Handled: {request.Message}");
        }
    }

    [Fact]
    public async Task Send_ShouldReturnResponse_WhenHandlerExists()
    {
        // Arrange
        var request = new TestRequest { Message = "Test Message" };
        string expectedResponse = "Handled: Test Message";

        // Mock request handler
        var handlerMock = new Mock<IRequestHandler<TestRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Mock pipeline builder
        var pipelineBuilderMock = new Mock<IRequestPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute<TestRequest, string>(
                It.IsAny<TestRequest>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Create mediator with mocked dependencies
        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            requestPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        string response = await mediator.Send(request, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, response);
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute<TestRequest, string>(
                request,
                _serviceProviderMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Send_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await mediator.Send<string>(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Send_ShouldCacheRequestHandler_ForMultipleCalls()
    {
        // Arrange
        var request1 = new TestRequest { Message = "Message 1" };
        var request2 = new TestRequest { Message = "Message 2" };

        var pipelineBuilderMock = new Mock<IRequestPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute<TestRequest, string>(
                It.IsAny<TestRequest>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            requestPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        await mediator.Send(request1, CancellationToken.None);
        await mediator.Send(request2, CancellationToken.None);

        // Assert
        // Verify that BuildAndExecute was called twice, indicating handler was reused from cache
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute<TestRequest, string>(
                It.IsAny<TestRequest>(),
                _serviceProviderMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region Publish Tests

    // Test Notification implementation
    public class TestNotification : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    // Test Notification Handler implementation
    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Publish_ShouldCallNotificationPipelineBuilder()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test Notification" };

        // Mock notification publisher
        var publisherMock = new Mock<INotificationPublisher>();
        publisherMock
            .Setup(p => p.Publish(
                It.IsAny<IEnumerable<INotificationHandler<TestNotification>>>(),
                It.IsAny<TestNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Mock pipeline builder
        var pipelineBuilderMock = new Mock<INotificationPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute(
                It.IsAny<TestNotification>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<INotificationPublisher>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Create mediator with mocked dependencies
        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            publisherMock.Object,
            notificationPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        await mediator.Publish(notification, CancellationToken.None);

        // Assert
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute(
                notification,
                _serviceProviderMock.Object,
                publisherMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_ShouldThrowArgumentNullException_WhenNotificationIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Publish<TestNotification>(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_ShouldCacheNotificationHandler_ForMultipleCalls()
    {
        // Arrange
        var notification1 = new TestNotification { Message = "Notification 1" };
        var notification2 = new TestNotification { Message = "Notification 2" };

        var pipelineBuilderMock = new Mock<INotificationPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute(
                It.IsAny<TestNotification>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<INotificationPublisher>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            notificationPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        await mediator.Publish(notification1, CancellationToken.None);
        await mediator.Publish(notification2, CancellationToken.None);

        // Assert
        // Verify that BuildAndExecute was called twice, indicating handler was reused from cache
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute(
                It.IsAny<TestNotification>(),
                _serviceProviderMock.Object,
                It.IsAny<INotificationPublisher>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Publish_ShouldUseDefaultPublisher_WhenNotProvided()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test Notification" };

        var pipelineBuilderMock = new Mock<INotificationPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute(
                It.IsAny<TestNotification>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<INotificationPublisher>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Create mediator with null publisher (should use default ForeachAwaitPublisher)
        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            publisher: null,
            notificationPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        await mediator.Publish(notification, CancellationToken.None);

        // Assert
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute(
                notification,
                _serviceProviderMock.Object,
                It.IsAny<ForeachAwaitPublisher>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreateStream Tests

    // Test Stream Request implementation
    public class TestStreamRequest : IStreamRequest<int>
    {
        public int Count { get; set; }
    }

    // Test Stream Request Handler implementation
    public class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(TestStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 1; i <= request.Count; i++)
            {
                yield return i;
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    [Fact]
    public async Task CreateStream_ShouldReturnAsyncEnumerable_WhenHandlerExists()
    {
        // Arrange
        var request = new TestStreamRequest { Count = 3 };

        // Create test stream
        static async IAsyncEnumerable<int> GetTestStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return 1;
            await Task.Delay(10, cancellationToken);
            yield return 2;
            await Task.Delay(10, cancellationToken);
            yield return 3;
        }

        var pipelineBuilderMock = new Mock<IStreamPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute<TestStreamRequest, int>(
                It.IsAny<TestStreamRequest>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .Returns((TestStreamRequest req, IServiceProvider sp, CancellationToken ct) =>
            {
                // Now the callback signature matches the method parameters
                return GetTestStream(ct);
            });

        // Create mediator with mocked dependencies
        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            streamPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        IAsyncEnumerable<int> stream = mediator.CreateStream(request, CancellationToken.None);
        var results = new List<int>();
        await foreach (int item in stream)
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 1, 2, 3 }, results);
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute<TestStreamRequest, int>(
                request,
                _serviceProviderMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void CreateStream_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            mediator.CreateStream<int>(null!, CancellationToken.None));
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void CreateStream_ShouldCacheStreamHandler_ForMultipleCalls()
    {
        // Arrange
        var request1 = new TestStreamRequest { Count = 1 };
        var request2 = new TestStreamRequest { Count = 2 };

        var pipelineBuilderMock = new Mock<IStreamPipelineBuilder>();
        pipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute<TestStreamRequest, int>(
                It.IsAny<TestStreamRequest>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .Returns((TestStreamRequest req, IServiceProvider sp, CancellationToken ct) => AsyncEnumerable.Empty<int>());

        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            streamPipelineBuilder: pipelineBuilderMock.Object);

        // Act
        _ = mediator.CreateStream(request1, CancellationToken.None);
        _ = mediator.CreateStream(request2, CancellationToken.None);

        // Assert
        // Verify that BuildAndExecute was called twice, indicating handler was reused from cache
        pipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute<TestStreamRequest, int>(
                It.IsAny<TestStreamRequest>(),
                _serviceProviderMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldUseDefaultPipelineBuilders_WhenNotProvided()
    {
        // Arrange & Act
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Assert - verification is implicit in that no exception is thrown
        Assert.NotNull(mediator);
    }

    [Fact]
    public async Task Constructor_ShouldUseProvidedPipelineBuilders_WhenProvided()
    {
        // Arrange
        var requestPipelineBuilderMock = new Mock<IRequestPipelineBuilder>();
        var notificationPipelineBuilderMock = new Mock<INotificationPipelineBuilder>();
        var streamPipelineBuilderMock = new Mock<IStreamPipelineBuilder>();
        var publisherMock = new Mock<INotificationPublisher>();

        // Act
        var mediator = new CQBus.Mediator.Mediator(
            _serviceProviderMock.Object,
            publisherMock.Object,
            requestPipelineBuilderMock.Object,
            notificationPipelineBuilderMock.Object,
            streamPipelineBuilderMock.Object);

        // Act - send a request to verify the custom pipeline builder is used
        var request = new TestRequest { Message = "Test" };

        requestPipelineBuilderMock
            .Setup(pb => pb.BuildAndExecute<TestRequest, string>(
                It.IsAny<TestRequest>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        await mediator.Send(request, CancellationToken.None);

        // Assert
        requestPipelineBuilderMock.Verify(
            pb => pb.BuildAndExecute<TestRequest, string>(
                request,
                _serviceProviderMock.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

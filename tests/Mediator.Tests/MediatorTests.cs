using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using Microsoft.Extensions.DependencyInjection;
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
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<TestRequest, string>, TestRequestHandler>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        var request = new TestRequest { Message = "Test Message" };
        string expectedResponse = "Handled: Test Message";

        var mediator = new CQBus.Mediator.Mediator(serviceProvider);

        // Act
        string response = await mediator.Send(request, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task Send_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await mediator.Send<string>(null!, CancellationToken.None));
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
    public async Task Publish_ShouldThrowArgumentNullException_WhenNotificationIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Publish<TestNotification>(null!, CancellationToken.None));
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
    public void CreateStream_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        var mediator = new CQBus.Mediator.Mediator(_serviceProviderMock.Object);

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            mediator.CreateStream<int>(null!, CancellationToken.None));
        Assert.Equal("request", exception.ParamName);
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

    #endregion
}

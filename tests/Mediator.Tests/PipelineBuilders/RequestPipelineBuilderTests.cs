using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.PipelineBuilders;
using CQBus.Mediator.Pipelines;
using Moq;

namespace Mediator.Tests.PipelineBuilders;

public class RequestPipelineBuilderTests
{
    // Test request implementing IRequest<string>
    public class TestRequest : IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    // Sample pipeline behavior that adds a prefix to responses
    public class PrefixPipelineBehavior(string prefix) : IPipelineBehavior<TestRequest, string>
    {
        public async ValueTask<string> Handle(
            TestRequest request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            string result = await next(cancellationToken);
            return $"{prefix}: {result}";
        }
    }

    // Sample pipeline behavior that adds a suffix to responses
    public class SuffixPipelineBehavior(string suffix) : IPipelineBehavior<TestRequest, string>
    {
        public async ValueTask<string> Handle(
            TestRequest request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            string result = await next(cancellationToken);
            return $"{result} {suffix}";
        }
    }

    [Fact]
    public async Task BuildAndExecute_WithNoRegisteredBehaviors_ShouldReturnHandlerResponse()
    {
        // Arrange
        var request = new TestRequest { Message = "Test" };
        string expectedResponse = "Handler processed: Test";

        // Mock handler
        var handlerMock = new Mock<IRequestHandler<TestRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<TestRequest, string>>());

        // Create pipeline builder
        var pipelineBuilder = new RequestPipelineBuilder();

        // Act
        string response = await pipelineBuilder.BuildAndExecute<TestRequest, string>(
            request,
            serviceProviderMock.Object,
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, response);
        handlerMock.Verify(
            h => h.Handle(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_WithRegisteredBehaviors_ShouldExecuteBehaviorsInReverseOrder()
    {
        // Arrange
        var request = new TestRequest { Message = "Test" };
        string handlerResponse = "Handler processed: Test";

        // Create behaviors
        var prefixBehavior = new PrefixPipelineBehavior("Prefix");
        var suffixBehavior = new SuffixPipelineBehavior("Suffix");
        var behaviors = new IPipelineBehavior<TestRequest, string>[] { prefixBehavior, suffixBehavior };

        // Mock handler
        var handlerMock = new Mock<IRequestHandler<TestRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(handlerResponse);

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(behaviors);

        // Create pipeline builder
        var pipelineBuilder = new RequestPipelineBuilder();

        // Act
        string response = await pipelineBuilder.BuildAndExecute<TestRequest, string>(
            request,
            serviceProviderMock.Object,
            CancellationToken.None);

        // Assert
        // When executed in reverse order, suffix should be applied first, then prefix
        // So expected result is "Prefix: Handler processed: Test Suffix"
        Assert.Equal("Prefix: Handler processed: Test Suffix", response);
        handlerMock.Verify(
            h => h.Handle(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldPassCancellationTokenToHandler()
    {
        // Arrange
        var request = new TestRequest { Message = "Test" };
        CancellationToken cancellationToken = CancellationToken.None;

        // Mock handler
        var handlerMock = new Mock<IRequestHandler<TestRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, cancellationToken))
            .ReturnsAsync("Response");

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<TestRequest, string>>());

        // Create pipeline builder
        var pipelineBuilder = new RequestPipelineBuilder();

        // Act
        await pipelineBuilder.BuildAndExecute<TestRequest, string>(
            request,
            serviceProviderMock.Object,
            cancellationToken);

        // Assert
        handlerMock.Verify(
            h => h.Handle(request, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldThrowServiceNotFoundException_WhenHandlerNotRegistered()
    {
        // Arrange
        var request = new TestRequest { Message = "Test" };

        // Mock service provider to return null for handler
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(null!);

        // Create pipeline builder
        var pipelineBuilder = new RequestPipelineBuilder();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipelineBuilder.BuildAndExecute<TestRequest, string>(
                request,
                serviceProviderMock.Object,
                CancellationToken.None));
    }
}

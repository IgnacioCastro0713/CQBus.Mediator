using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.PipelineBuilders;
using CQBus.Mediator.Pipelines;
using Moq;

namespace Mediator.Tests.PipelineBuilders;

public class StreamPipelineBuilderTests
{
    // Test request class implementing IStreamRequest<string>
    public class TestStreamRequest : IStreamRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    // Test handler implementation that returns a simple stream of strings
    public class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return $"Response1: {request.Value}";
            await Task.Delay(10, cancellationToken);
            yield return $"Response2: {request.Value}";
            await Task.Delay(10, cancellationToken);
            yield return $"Response3: {request.Value}";
        }
    }

    // Test pipeline behavior that adds a prefix to each item
    public class PrefixStreamBehavior(string prefix) : IStreamPipelineBehavior<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            StreamHandlerDelegate<string> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerable<string> result = next(cancellationToken);

            await foreach (string item in result.WithCancellation(cancellationToken))
            {
                yield return $"{prefix} | {item}";
            }
        }
    }

    // Test pipeline behavior that adds a suffix to each item
    public class SuffixStreamBehavior(string suffix) : IStreamPipelineBehavior<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            StreamHandlerDelegate<string> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerable<string> result = next(cancellationToken);

            await foreach (string item in result.WithCancellation(cancellationToken))
            {
                yield return $"{item} | {suffix}";
            }
        }
    }

    // Helper method to generate a mock stream result
    private static async IAsyncEnumerable<string> GetTestStream(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= 3; i++)
        {
            yield return $"{prefix} Item {i}";
            await Task.Delay(10, cancellationToken);
        }
    }

    [Fact]
    public async Task BuildAndExecute_WithNoRegisteredBehaviors_ShouldReturnHandlerResponse()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };

        // Mock handler
        var handlerMock = new Mock<IStreamRequestHandler<TestStreamRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .Returns((TestStreamRequest req, CancellationToken ct) => GetTestStream("Handler", ct));

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>>)))
            .Returns(Array.Empty<IStreamPipelineBehavior<TestStreamRequest, string>>());

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act
        var results = new List<string>();
        await foreach (string item in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
            request,
            serviceProviderMock.Object,
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal("Handler Item 1", results[0]);
        Assert.Equal("Handler Item 2", results[1]);
        Assert.Equal("Handler Item 3", results[2]);
        handlerMock.Verify(
            h => h.Handle(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_WithRegisteredBehaviors_ShouldExecuteBehaviorsInReverseOrder()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };

        // Create behaviors
        var prefixBehavior = new PrefixStreamBehavior("Prefix");
        var suffixBehavior = new SuffixStreamBehavior("Suffix");
        var behaviors = new IStreamPipelineBehavior<TestStreamRequest, string>[] { prefixBehavior, suffixBehavior };

        // Mock handler
        var handlerMock = new Mock<IStreamRequestHandler<TestStreamRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .Returns((TestStreamRequest req, CancellationToken ct) => GetTestStream("Handler", ct));

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>>)))
            .Returns(behaviors);

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act
        var results = new List<string>();
        await foreach (string item in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
            request,
            serviceProviderMock.Object,
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        // The behaviors should be applied in reverse order (suffix first, then prefix)
        Assert.Equal("Prefix | Handler Item 1 | Suffix", results[0]);
        Assert.Equal("Prefix | Handler Item 2 | Suffix", results[1]);
        Assert.Equal("Prefix | Handler Item 3 | Suffix", results[2]);
        handlerMock.Verify(
            h => h.Handle(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldPassCancellationTokenToHandlerAndBehaviors()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        // Mock handler
        var handlerMock = new Mock<IStreamRequestHandler<TestStreamRequest, string>>();
        handlerMock
            .Setup(h => h.Handle(request, cancellationToken))
            .Returns((TestStreamRequest req, CancellationToken ct) => GetTestStream("Handler", ct));

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(handlerMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>>)))
            .Returns(Array.Empty<IStreamPipelineBehavior<TestStreamRequest, string>>());

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act
        var results = new List<string>();
        await foreach (string item in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
            request,
            serviceProviderMock.Object,
            cancellationToken))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        handlerMock.Verify(
            h => h.Handle(request, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldRespectCancellation()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };
        var cts = new CancellationTokenSource();

        // Create a handler that will check for cancellation
        var handler = new TestStreamRequestHandler();

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(handler);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>>)))
            .Returns(Array.Empty<IStreamPipelineBehavior<TestStreamRequest, string>>());

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act & Assert
        var results = new List<string>();
        Exception? exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (string item in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
                request,
                serviceProviderMock.Object,
                cts.Token))
            {
                results.Add(item);
                if (results.Count == 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.NotNull(exception);
        Assert.IsType<TaskCanceledException>(exception);
        Assert.Single(results);
    }

    [Fact]
    public async Task BuildAndExecute_ShouldThrowServiceNotFoundException_WhenHandlerNotRegistered()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };

        // Mock service provider to throw when GetService is called
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(null!);

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (string _ in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
                request,
                serviceProviderMock.Object,
                CancellationToken.None))
            {
                // This code should not be executed
            }
        });

        Assert.Contains("No service for type", exception.Message);
    }

    [Fact]
    public async Task BuildAndExecute_WithMultipleBehaviors_ShouldExecuteThemInCorrectOrder()
    {
        // Arrange
        var request = new TestStreamRequest { Value = "Test" };

        // Create behaviors with different prefixes to track execution order
        var behavior1 = new PrefixStreamBehavior("First");
        var behavior2 = new PrefixStreamBehavior("Second");
        var behavior3 = new PrefixStreamBehavior("Third");
        var behaviors = new IStreamPipelineBehavior<TestStreamRequest, string>[] { behavior1, behavior2, behavior3 };

        // Create handler
        var handler = new TestStreamRequestHandler();

        // Mock service provider
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IStreamRequestHandler<TestStreamRequest, string>)))
            .Returns(handler);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>>)))
            .Returns(behaviors);

        // Create pipeline builder
        var pipelineBuilder = new StreamPipelineBuilder();

        // Act
        var results = new List<string>();
        await foreach (string item in pipelineBuilder.BuildAndExecute<TestStreamRequest, string>(
            request,
            serviceProviderMock.Object,
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert - behaviors should be applied in reverse order
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.StartsWith("First | Second | Third | Response", r));
    }
}

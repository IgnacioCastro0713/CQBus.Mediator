using System.Runtime.CompilerServices;
using CQBus.Mediator.Messages;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests.Pipelines;

public class StreamPipelineBehaviorTests
{
    // Test request class implementing IStreamRequest<string>
    public class TestStreamRequest : IStreamRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    // Test implementation of IStreamPipelineBehavior that logs before and after each item
    public class LoggingStreamBehavior : IStreamPipelineBehavior<TestStreamRequest, string>
    {
        private readonly List<string> _log = new();

        public IReadOnlyList<string> Log => _log;

        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            StreamHandlerDelegate<string> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _log.Add($"Before: {request.Value}");

            IAsyncEnumerable<string> result = next(cancellationToken);

            await foreach (string item in result.WithCancellation(cancellationToken))
            {
                _log.Add($"Item: {item}");
                yield return $"Modified: {item}";
            }

            _log.Add($"After: {request.Value}");
        }
    }

    // Test implementation of IStreamPipelineBehavior that transforms items
    public class UpperCaseStreamBehavior : IStreamPipelineBehavior<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            StreamHandlerDelegate<string> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerable<string> result = next(cancellationToken);

            await foreach (string item in result.WithCancellation(cancellationToken))
            {
                yield return item.ToUpperInvariant();
            }
        }
    }

    // Test implementation of IStreamPipelineBehavior that filters items
    public class FilterStreamBehavior : IStreamPipelineBehavior<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            TestStreamRequest request,
            StreamHandlerDelegate<string> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerable<string> result = next(cancellationToken);

            await foreach (string item in result.WithCancellation(cancellationToken))
            {
                if (!item.Contains("skip"))
                {
                    yield return item;
                }
            }
        }
    }

    // Helper method to generate test stream data
    private static async IAsyncEnumerable<string> GetTestStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "item1";
        await Task.Delay(10, cancellationToken);
        yield return "item2";
        await Task.Delay(10, cancellationToken);
        yield return "skip-this-item";
        await Task.Delay(10, cancellationToken);
        yield return "item3";
    }

    [Fact]
    public async Task Handle_ShouldProcessEachStreamItem()
    {
        // Arrange
        var behavior = new LoggingStreamBehavior();
        var request = new TestStreamRequest { Value = "test-request" };
        StreamHandlerDelegate<string> next = GetTestStream;

        // Act
        var results = new List<string>();
        await foreach (string item in behavior.Handle(request, next, CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.StartsWith("Modified:", r));
        Assert.Equal(6, behavior.Log.Count); // Before + 3 items + After
        Assert.Equal("Before: test-request", behavior.Log[0]);
        Assert.Equal("After: test-request", behavior.Log[^1]);
    }

    [Fact]
    public async Task Handle_ShouldSupportMultipleNestedBehaviors()
    {
        // Arrange
        var loggingBehavior = new LoggingStreamBehavior();
        var upperCaseBehavior = new UpperCaseStreamBehavior();
        var filterBehavior = new FilterStreamBehavior();

        var request = new TestStreamRequest { Value = "test-chain" };

        // Start with the innermost handler
        StreamHandlerDelegate<string> pipeline = GetTestStream;

        // Build the pipeline in the correct order (innermost to outermost)
        // Each lambda must capture the current value of pipeline, not the variable itself
        StreamHandlerDelegate<string> filterPipeline = pipeline; // Capture the current value
        pipeline = ct => filterBehavior.Handle(request, _ => filterPipeline(ct), ct);

        StreamHandlerDelegate<string> upperPipeline = pipeline; // Capture the current value
        pipeline = ct => upperCaseBehavior.Handle(request, _ => upperPipeline(ct), ct);

        StreamHandlerDelegate<string> logPipeline = pipeline; // Capture the current value
        pipeline = ct => loggingBehavior.Handle(request, _ => logPipeline(ct), ct);

        // Act
        var results = new List<string>();
        await foreach (string item in pipeline(CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal("Modified: ITEM1", results[0]);
        Assert.Equal("Modified: ITEM2", results[1]);
        Assert.Equal("Modified: ITEM3", results[2]);

        // Check that the logging behavior processed the events in the correct order
        Assert.Equal(5, loggingBehavior.Log.Count);
        Assert.Equal("Before: test-chain", loggingBehavior.Log[0]);
        Assert.Contains("Item: ITEM1", loggingBehavior.Log);
        Assert.Contains("Item: ITEM2", loggingBehavior.Log);
        Assert.Contains("Item: ITEM3", loggingBehavior.Log);
        Assert.Equal("After: test-chain", loggingBehavior.Log[^1]);
    }

    [Fact]
    public async Task Handle_ShouldRespectCancellationToken()
    {
        // Arrange
        var behavior = new LoggingStreamBehavior();
        var request = new TestStreamRequest { Value = "test-cancellation" };

        // Create a cancellation token source that cancels after the first item
        var cts = new CancellationTokenSource();

        StreamHandlerDelegate<string> next = GetLongRunningStream;

        // Act & Assert
        var results = new List<string>();
        Exception? exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (string item in behavior.Handle(request, next, cts.Token))
            {
                results.Add(item);
                if (results.Count == 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.IsType<TaskCanceledException>(exception);
        Assert.Single(results);
    }

    private static async IAsyncEnumerable<string> GetLongRunningStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 10; i++)
        {
            yield return $"item{i}";
            await Task.Delay(50, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [Fact]
    public async Task Handle_WithDependencyInjection_ShouldResolveBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IStreamPipelineBehavior<TestStreamRequest, string>, LoggingStreamBehavior>();
        services.AddTransient<IStreamPipelineBehavior<TestStreamRequest, string>, UpperCaseStreamBehavior>();
        services.AddTransient<IStreamPipelineBehavior<TestStreamRequest, string>, FilterStreamBehavior>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        var request = new TestStreamRequest { Value = "test-di" };
        IEnumerable<IStreamPipelineBehavior<TestStreamRequest, string>> behaviors =
            serviceProvider.GetServices<IStreamPipelineBehavior<TestStreamRequest, string>>();

        // Build pipeline
        StreamHandlerDelegate<string> pipeline = GetTestStream;
        foreach (IStreamPipelineBehavior<TestStreamRequest, string> behavior in behaviors.Reverse())
        {
            StreamHandlerDelegate<string> next = pipeline;
            pipeline = ct => behavior.Handle(request, _ => next(ct), ct);
        }

        // Act
        var results = new List<string>();
        await foreach (string item in pipeline(CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, item => Assert.Equal(item, item));
        Assert.DoesNotContain(results, item => item.Contains("skip"));
    }
}

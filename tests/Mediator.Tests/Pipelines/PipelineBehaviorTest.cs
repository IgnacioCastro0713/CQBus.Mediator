using CQBus.Mediator.Pipelines;

namespace Mediator.Tests.Pipelines;

// A sample implementation of IPipelineBehavior<TRequest, TResponse> for testing purposes
public class PipelineBehaviorTest : IPipelineBehavior<SampleRequest, string>
{
    public async ValueTask<string> Handle(
        SampleRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        // Pre-processing logic
        string preProcessed = $"PreProcessed: {request.Data}";

        // Call the next delegate
        string result = await next();

        // Post-processing logic
        return $"{preProcessed} | {result}";
    }
}

// A sample request class for testing purposes
public class SampleRequest
{
    public string Data { get; set; }
}

// A generic implementation for testing different types
public class GenericPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Simply call the next delegate
        return await next();
    }
}

// A generic implementation of a request for testing purposes
public class TestRequest<T>
{
    public T Data { get; set; }
}

public class PipelineBehaviorTests
{
    [Fact]
    public void SamplePipelineBehavior_ShouldImplementIPipelineBehavior()
    {
        // Arrange
        var behavior = new PipelineBehaviorTest();

        // Act & Assert
        Assert.IsAssignableFrom<IPipelineBehavior<SampleRequest, string>>(behavior);
    }

    [Fact]
    public async Task Handle_ShouldProcessRequestAndResponse()
    {
        // Arrange
        var behavior = new PipelineBehaviorTest();
        var request = new SampleRequest { Data = "Test" };
        RequestHandlerDelegate<string> next = (_) => new ValueTask<string>(Task.FromResult("Handler Response"));

        // Act
        string response = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        Assert.Equal("PreProcessed: Test | Handler Response", response);
    }

    [Fact]
    public async Task GenericPipelineBehavior_ShouldHandleDifferentGenericTypes()
    {
        // Arrange
        var intBehavior = new GenericPipelineBehavior<TestRequest<int>, int>();
        var boolBehavior = new GenericPipelineBehavior<TestRequest<bool>, bool>();

        var intRequest = new TestRequest<int> { Data = 42 };
        var boolRequest = new TestRequest<bool> { Data = true };

        RequestHandlerDelegate<int> intNext = (_) => new ValueTask<int>(Task.FromResult(100));
        RequestHandlerDelegate<bool> boolNext = (_) => new ValueTask<bool>(Task.FromResult(false));

        // Act
        int intResponse = await intBehavior.Handle(intRequest, intNext, CancellationToken.None);
        bool boolResponse = await boolBehavior.Handle(boolRequest, boolNext, CancellationToken.None);

        // Assert
        Assert.Equal(100, intResponse);
        Assert.False(boolResponse);
    }

    [Fact]
    public void IPipelineBehavior_ShouldHandleNullValuesGracefully()
    {
        // Arrange
        IPipelineBehavior<SampleRequest, string> behavior = null!;

        // Act & Assert
        Assert.Null(behavior);
    }

    [Fact]
    public void IPipelineBehavior_ShouldSupportComplexGenericTypes()
    {
        // Arrange
        var behavior =
            new GenericPipelineBehavior<TestRequest<Dictionary<string, List<int>>>, Dictionary<string, List<int>>>();

        // Act & Assert
        Assert
            .IsAssignableFrom<
                IPipelineBehavior<TestRequest<Dictionary<string, List<int>>>, Dictionary<string, List<int>>>>(behavior);
    }

    [Fact]
    public async Task IPipelineBehavior_ShouldSupportInheritanceInGenericTypes()
    {
        // Arrange
        var baseBehavior = new GenericPipelineBehavior<TestRequest<BaseClass>, BaseClass>();
        var derivedBehavior = new GenericPipelineBehavior<TestRequest<DerivedClass>, DerivedClass>();

        var baseRequest = new TestRequest<BaseClass> { Data = new BaseClass() };
        var derivedRequest = new TestRequest<DerivedClass> { Data = new DerivedClass() };

        RequestHandlerDelegate<BaseClass> baseNext = (_) => new ValueTask<BaseClass>(Task.FromResult(new BaseClass()));
        RequestHandlerDelegate<DerivedClass> derivedNext = (_) => new ValueTask<DerivedClass>(Task.FromResult(new DerivedClass()));

        // Act
        BaseClass baseResponse = await baseBehavior.Handle(baseRequest, baseNext, CancellationToken.None);
        DerivedClass derivedResponse =
            await derivedBehavior.Handle(derivedRequest, derivedNext, CancellationToken.None);

        // Assert
        Assert.IsType<BaseClass>(baseResponse);
        Assert.IsType<DerivedClass>(derivedResponse);
    }
}

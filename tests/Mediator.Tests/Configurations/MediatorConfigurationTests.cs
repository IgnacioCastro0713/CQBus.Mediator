using System.Reflection;
using CQBus.Mediator.Configurations;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests.Configurations;

public class MediatorConfigurationTests
{
    #region Mock Types for Testing

    // Mock types for IPipelineBehavior tests
    public class MockRequest { }
    public class MockResponse { }
    public class MockPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    // Mock types for IStreamPipelineBehavior tests
    public class MockStreamPipelineBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (TResponse item in next(cancellationToken).WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }

    // Non-generic type for negative tests
    public class NonGenericType { }

    // Type that doesn't implement required interface for negative tests
    public class TypeWithoutRequiredInterface<T1, T2> { }

    #endregion

    [Fact]
    public void AssembliesToRegister_ShouldBeEmptyByDefault()
    {
        // Arrange
        var options = new MediatorConfiguration();

        // Act & Assert
        Assert.Empty(options.AssembliesToRegister);
    }

    [Fact]
    public void BehaviorsToRegister_ShouldBeEmptyByDefault()
    {
        // Arrange
        var options = new MediatorConfiguration();

        // Act & Assert
        Assert.Empty(options.BehaviorsToRegister);
    }

    [Fact]
    public void StreamBehaviorsToRegister_ShouldBeEmptyByDefault()
    {
        // Arrange
        var options = new MediatorConfiguration();

        // Act & Assert
        Assert.Empty(options.StreamBehaviorsToRegister);
    }

    [Fact]
    public void Lifetime_ShouldDefaultToTransient()
    {
        // Arrange
        var options = new MediatorConfiguration();

        // Act & Assert
        Assert.Equal(ServiceLifetime.Scoped, options.ServiceLifetime);
    }

    [Fact]
    public void PublisherStrategyType_ShouldDefaultToForeachAwaitPublisher()
    {
        // Arrange
        var options = new MediatorConfiguration();

        // Act & Assert
        Assert.Equal(typeof(ForeachAwaitPublisher), options.PublisherStrategyType);
    }

    [Fact]
    public void RegisterServicesFromAssembly_ShouldAddAssemblyToAssembliesToRegister()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Assembly assembly = typeof(MediatorConfigurationTests).Assembly;

        // Act
        options.RegisterServicesFromAssembly(assembly);

        // Assert
        Assert.Single(options.AssembliesToRegister);
        Assert.Contains(assembly, options.AssembliesToRegister);
    }

    [Fact]
    public void RegisterServicesFromAssembly_ShouldSupportMultipleAssemblies()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Assembly assembly1 = typeof(MediatorConfigurationTests).Assembly;
        Assembly assembly2 = typeof(string).Assembly;

        // Act
        options.RegisterServicesFromAssembly(assembly1);
        options.RegisterServicesFromAssembly(assembly2);

        // Assert
        Assert.Equal(2, options.AssembliesToRegister.Count);
        Assert.Contains(assembly1, options.AssembliesToRegister);
        Assert.Contains(assembly2, options.AssembliesToRegister);
    }

    [Fact]
    public void AddBehavior_ShouldAddServiceDescriptorToBehaviorsToRegister()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IServiceProvider);
        Type implementationType = typeof(ServiceProvider);

        // Act
        options.AddBehavior(serviceType, implementationType);

        // Assert
        Assert.Single(options.BehaviorsToRegister);
        ServiceDescriptor descriptor = options.BehaviorsToRegister[0];
        Assert.Equal(serviceType, descriptor.ServiceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBehavior_ShouldSupportCustomServiceLifetime()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IServiceProvider);
        Type implementationType = typeof(ServiceProvider);

        // Act
        options.AddBehavior(serviceType, implementationType, ServiceLifetime.Singleton);

        // Assert
        Assert.Single(options.BehaviorsToRegister);
        ServiceDescriptor descriptor = options.BehaviorsToRegister[0];
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddBehavior_ShouldSupportMultipleBehaviors()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType1 = typeof(IServiceProvider);
        Type implementationType1 = typeof(ServiceProvider);
        Type serviceType2 = typeof(IList<>);
        Type implementationType2 = typeof(List<>);

        // Act
        options.AddBehavior(serviceType1, implementationType1);
        options.AddBehavior(serviceType2, implementationType2, ServiceLifetime.Scoped);

        // Assert
        Assert.Equal(2, options.BehaviorsToRegister.Count);
        ServiceDescriptor descriptor1 = options.BehaviorsToRegister[0];
        ServiceDescriptor descriptor2 = options.BehaviorsToRegister[1];

        Assert.Equal(serviceType1, descriptor1.ServiceType);
        Assert.Equal(implementationType1, descriptor1.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor1.Lifetime);

        Assert.Equal(serviceType2, descriptor2.ServiceType);
        Assert.Equal(implementationType2, descriptor2.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor2.Lifetime);
    }

    [Fact]
    public void AddBehavior_ShouldThrowExceptionForNullServiceType()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type implementationType = typeof(ServiceProvider);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            options.AddBehavior(null!, implementationType));
    }

    [Fact]
    public void AddBehavior_ShouldThrowExceptionForNullImplementationType()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IServiceProvider);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            options.AddBehavior(serviceType, null!));
    }

    [Fact]
    public void AddBehavior_ShouldSupportGenericTypes()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IList<>);
        Type implementationType = typeof(List<>);

        // Act
        options.AddBehavior(serviceType, implementationType);

        // Assert
        Assert.Single(options.BehaviorsToRegister);
        ServiceDescriptor descriptor = options.BehaviorsToRegister[0];
        Assert.Equal(serviceType, descriptor.ServiceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenBehavior_ShouldAddServiceDescriptorForGenericBehavior()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type behaviorType = typeof(MockPipelineBehavior<,>);

        // Act
        MediatorConfiguration result = options.AddOpenBehavior(behaviorType);

        // Assert
        Assert.Same(options, result); // Verify fluent interface returns the same instance
        Assert.Single(options.BehaviorsToRegister);
        ServiceDescriptor descriptor = options.BehaviorsToRegister[0];
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(behaviorType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_ShouldSupportCustomServiceLifetime()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type behaviorType = typeof(MockPipelineBehavior<,>);

        // Act
        options.AddOpenBehavior(behaviorType, ServiceLifetime.Scoped);

        // Assert
        Assert.Single(options.BehaviorsToRegister);
        ServiceDescriptor descriptor = options.BehaviorsToRegister[0];
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_ShouldThrowException_WhenBehaviorTypeIsNotGeneric()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type nonGenericType = typeof(NonGenericType);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            options.AddOpenBehavior(nonGenericType));

        Assert.Contains("must be generic", exception.Message);
    }

    [Fact]
    public void AddOpenBehavior_ShouldThrowException_WhenBehaviorTypeDoesNotImplementIPipelineBehavior()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type invalidType = typeof(TypeWithoutRequiredInterface<,>);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            options.AddOpenBehavior(invalidType));

        Assert.Contains("must implement", exception.Message);
    }

    [Fact]
    public void AddStreamBehavior_ShouldAddServiceDescriptorToStreamBehaviorsToRegister()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IStreamPipelineBehavior<MockRequest, MockResponse>);
        Type implementationType = typeof(MockStreamPipelineBehavior<MockRequest, MockResponse>);

        // Act
        MediatorConfiguration result = options.AddStreamBehavior(serviceType, implementationType);

        // Assert
        Assert.Same(options, result); // Verify fluent interface returns the same instance
        Assert.Single(options.StreamBehaviorsToRegister);
        ServiceDescriptor descriptor = options.StreamBehaviorsToRegister[0];
        Assert.Equal(serviceType, descriptor.ServiceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddStreamBehavior_ShouldSupportCustomServiceLifetime()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type serviceType = typeof(IStreamPipelineBehavior<MockRequest, MockResponse>);
        Type implementationType = typeof(MockStreamPipelineBehavior<MockRequest, MockResponse>);

        // Act
        options.AddStreamBehavior(serviceType, implementationType, ServiceLifetime.Singleton);

        // Assert
        Assert.Single(options.StreamBehaviorsToRegister);
        ServiceDescriptor descriptor = options.StreamBehaviorsToRegister[0];
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenStreamBehavior_ShouldAddServiceDescriptorForGenericStreamBehavior()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type behaviorType = typeof(MockStreamPipelineBehavior<,>);

        // Act
        MediatorConfiguration result = options.AddOpenStreamBehavior(behaviorType);

        // Assert
        Assert.Same(options, result); // Verify fluent interface returns the same instance
        Assert.Single(options.StreamBehaviorsToRegister);
        ServiceDescriptor descriptor = options.StreamBehaviorsToRegister[0];
        Assert.Equal(typeof(IStreamPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(behaviorType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenStreamBehavior_ShouldSupportCustomServiceLifetime()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type behaviorType = typeof(MockStreamPipelineBehavior<,>);

        // Act
        options.AddOpenStreamBehavior(behaviorType, ServiceLifetime.Scoped);

        // Assert
        Assert.Single(options.StreamBehaviorsToRegister);
        ServiceDescriptor descriptor = options.StreamBehaviorsToRegister[0];
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenStreamBehavior_ShouldThrowException_WhenBehaviorTypeIsNotGeneric()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type nonGenericType = typeof(NonGenericType);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            options.AddOpenStreamBehavior(nonGenericType));

        Assert.Contains("must be generic", exception.Message);
    }

    [Fact]
    public void AddOpenStreamBehavior_ShouldThrowException_WhenBehaviorTypeDoesNotImplementIStreamPipelineBehavior()
    {
        // Arrange
        var options = new MediatorConfiguration();
        Type invalidType = typeof(TypeWithoutRequiredInterface<,>);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            options.AddOpenStreamBehavior(invalidType));

        Assert.Contains("must implement", exception.Message);
    }
}

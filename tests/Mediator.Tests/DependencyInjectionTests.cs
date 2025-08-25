using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CQBus.Mediator;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests;

public class DependencyInjectionTests
{
    #region Test Types
    // Sample request class
    [ExcludeFromCodeCoverage]
    public class TestRequest : IRequest<string> { }

    // Sample request handler
    [ExcludeFromCodeCoverage]
    public class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> Handle(TestRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult("test");
    }

    // Sample notification
    [ExcludeFromCodeCoverage]
    public class TestNotification : INotification { }

    // Sample notification handler
    [ExcludeFromCodeCoverage]
    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    // Sample stream request
    [ExcludeFromCodeCoverage]
    public class TestStreamRequest : IStreamRequest<int> { }

    // Sample stream request handler
    [ExcludeFromCodeCoverage]
    public class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(TestStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Delay(10, cancellationToken);
            yield return 2;
        }
    }

    // Sample pipeline behavior
    [ExcludeFromCodeCoverage]
    public class TestPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            return await next(cancellationToken);
        }
    }

    // Sample stream pipeline behavior
    [ExcludeFromCodeCoverage]
    public class TestStreamPipelineBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
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

    // Custom notification publisher
    [ExcludeFromCodeCoverage]
    public class TestPublisher : INotificationPublisher
    {
        public ValueTask Publish<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken) where TNotification : INotification
        {
            return ValueTask.CompletedTask;
        }
    }
    #endregion

    [Fact]
    public void AddMediator_ShouldRegisterRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Core mediator services
        Assert.NotNull(serviceProvider.GetService<IMediator>());
        Assert.NotNull(serviceProvider.GetService<ISender>());
        Assert.NotNull(serviceProvider.GetService<IPublisher>());

        // Handler registrations
        Assert.NotNull(serviceProvider.GetService<IRequestHandler<TestRequest, string>>());
        Assert.NotNull(serviceProvider.GetService<INotificationHandler<TestNotification>>());
        Assert.NotNull(serviceProvider.GetService<IStreamRequestHandler<TestStreamRequest, int>>());

        // Default publisher
        Assert.IsType<ForeachAwaitPublisher>(serviceProvider.GetService<INotificationPublisher>());
    }

    [Fact]
    public void AddMediator_ShouldThrowException_WhenNoAssembliesRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            services.AddMediator(_ => { }));

        Assert.Equal("assembliesToRegister", exception.ParamName);
    }

    [Fact]
    public void AddMediator_ShouldRegisterCustomPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.PublisherStrategyType = typeof(TestPublisher);
        });

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        Assert.IsType<TestPublisher>(serviceProvider.GetService<INotificationPublisher>());
    }

    [Fact]
    public void AddMediator_ShouldThrowException_WhenInvalidPublisherType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator(config =>
            {
                config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                config.PublisherStrategyType = typeof(string); // Not a publisher
            }));
    }

    [Fact]
    public void AddMediator_ShouldRegisterWithCustomLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.ServiceLifetime = ServiceLifetime.Singleton;
        });

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMediator_ShouldRegisterBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddBehavior(
                typeof(IPipelineBehavior<TestRequest, string>),
                typeof(TestPipelineBehavior<TestRequest, string>));
        });

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IEnumerable<IPipelineBehavior<TestRequest, string>> behaviors = serviceProvider.GetServices<IPipelineBehavior<TestRequest, string>>();
        Assert.Contains(behaviors, b => b.GetType() == typeof(TestPipelineBehavior<TestRequest, string>));
    }

    [Fact]
    public void AddMediator_ShouldRegisterOpenBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(typeof(TestStreamRequest).Assembly);
            config.AddOpenBehavior(typeof(TestPipelineBehavior<,>));
        });

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IEnumerable<IPipelineBehavior<TestRequest, string>> behaviors = serviceProvider.GetServices<IPipelineBehavior<TestRequest, string>>();
        Assert.Contains(behaviors, b => b.GetType().GetGenericTypeDefinition() == typeof(TestPipelineBehavior<,>));
    }

    [Fact]
    public void AddMediator_ShouldRegisterStreamBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            // Remove the whole assembly registration to avoid registering problematic types
            // and just register the specific types we need
            config.RegisterServicesFromAssembly(typeof(DependencyInjectionTests).Assembly);

            // Instead, manually register the handler needed for testing
            services.AddTransient<IStreamRequestHandler<TestStreamRequest, int>, TestStreamRequestHandler>();

            // Register a closed generic behavior with its correct closed generic service interface
            config.AddStreamBehavior(
                typeof(IStreamPipelineBehavior<TestStreamRequest, int>),
                typeof(TestStreamPipelineBehavior<TestStreamRequest, int>));
        });

        // Assert - Use GetRequiredService to avoid trying to instantiate all behaviors
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IStreamPipelineBehavior<TestStreamRequest, int> behavior = serviceProvider.GetRequiredService<IStreamPipelineBehavior<TestStreamRequest, int>>();
        Assert.IsType<TestStreamPipelineBehavior<TestStreamRequest, int>>(behavior);
    }

    [Fact]
    public void AddMediator_ShouldRegisterOpenStreamBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddOpenStreamBehavior(typeof(TestStreamPipelineBehavior<,>));
        });

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IEnumerable<IStreamPipelineBehavior<TestStreamRequest, int>> behaviors = serviceProvider.GetServices<IStreamPipelineBehavior<TestStreamRequest, int>>();
        Assert.Contains(behaviors, b => b.GetType().GetGenericTypeDefinition() == typeof(TestStreamPipelineBehavior<,>));
    }

    [Fact]
    public void AddMediator_ShouldUseDefaultScope_WhenNotSpecified()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddMediator_ShouldRegisterHandlersFromMultipleAssemblies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.RegisterServicesFromAssembly(typeof(string).Assembly);
        });

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IRequestHandler<TestRequest, string>>());
    }

    [Fact]
    public void AddMediator_ShouldPreventDuplicateServiceRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IMediator, CQBus.Mediator.Mediator>(); // Pre-register service

        // Act
        services.AddMediator(config =>
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Assert
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IMediator)));
    }
}

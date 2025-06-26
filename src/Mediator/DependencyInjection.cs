using System.Reflection;
using CQBus.Mediator.Configurations;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CQBus.Mediator;

public static class DependencyInjection
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorConfiguration> configurations)
    {
        var configurationOptions = new MediatorConfiguration();

        configurations.Invoke(configurationOptions);

        services.TryAddMediator(configurationOptions);

        services.AddPipelinesBuilders();

        services.TryAddHandlers(configurationOptions.AssembliesToRegister, configurationOptions.ServiceLifetime);

        services.TryAddPublisher(configurationOptions.PublisherStrategyType, configurationOptions.ServiceLifetime);

        services.TryAddBehaviours(configurationOptions);

        return services;
    }

    private static void TryAddMediator(this IServiceCollection services, MediatorConfiguration configurationOptions)
    {
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), configurationOptions.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), configurationOptions.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), configurationOptions.ServiceLifetime));
    }

    private static void AddPipelinesBuilders(this IServiceCollection services)
    {
        services.AddSingleton<IRequestPipelineBuilder, RequestPipelineBuilder>();
        services.AddSingleton<INotificationPipelineBuilder, NotificationPipelineBuilder>();
        services.AddSingleton<IStreamPipelineBuilder, StreamPipelineBuilder>();
    }

    private static void TryAddHandlers(
        this IServiceCollection services,
        List<Assembly> assembliesToRegister,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (assembliesToRegister == null || !assembliesToRegister.Any())
        {
            throw new ArgumentNullException(nameof(assembliesToRegister), "At least one assembly must be provided for handler registration.");
        }

        Type requestHandlerType = typeof(IRequestHandler<,>);
        Type streamHandlerType = typeof(IStreamRequestHandler<,>);
        Type notificationHandlerType = typeof(INotificationHandler<>);

        var handlers = assembliesToRegister
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces(), (type, iFace) => new { type, iface = iFace })
            .Where(t => t.iface.IsGenericType && (
                t.iface.GetGenericTypeDefinition() == requestHandlerType ||
                t.iface.GetGenericTypeDefinition() == streamHandlerType ||
                t.iface.GetGenericTypeDefinition() == notificationHandlerType))
            .ToList();

        foreach (var handler in handlers)
        {
            services.TryAddEnumerable(new ServiceDescriptor(handler.iface, handler.type, serviceLifetime));
        }
    }

    private static void TryAddPublisher(
        this IServiceCollection services,
        Type publisherStrategyType,
        ServiceLifetime serviceLifetime)
    {
        if (!typeof(INotificationPublisher).IsAssignableFrom(publisherStrategyType))
        {
            throw new InvalidOperationException($"{publisherStrategyType.Name} must implement {nameof(INotificationPublisher)} interface.");
        }

        services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), publisherStrategyType, serviceLifetime));
    }

    private static void TryAddBehaviours(this IServiceCollection services, MediatorConfiguration configurationOptions)
    {
        foreach (ServiceDescriptor serviceDescriptor in configurationOptions.BehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }

        foreach (ServiceDescriptor serviceDescriptor in configurationOptions.StreamBehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }
    }
}

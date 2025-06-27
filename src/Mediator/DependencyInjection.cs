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
    private static readonly Type RequestHandlerType = typeof(IRequestHandler<,>);
    private static readonly Type StreamHandlerType = typeof(IStreamRequestHandler<,>);
    private static readonly Type NotificationHandlerType = typeof(INotificationHandler<>);
    private static readonly Type NotificationPublisherType = typeof(INotificationPublisher);

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

        services.TryAddBehaviors(configurationOptions);

        return services;
    }

    private static void TryAddMediator(this IServiceCollection services, MediatorConfiguration configurationOptions)
    {
        services.TryAdd(ServiceDescriptor.Describe(typeof(IMediator), typeof(Mediator), configurationOptions.ServiceLifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), configurationOptions.ServiceLifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), configurationOptions.ServiceLifetime));
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

        var handlers = assembliesToRegister
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsInterface: false })
            .Select(t => new
            {
                Type = t,
                Interfaces = t.GetInterfaces()
                    .Where(i => i.IsGenericType && IsHandlerInterface(i.GetGenericTypeDefinition()))
                    .ToList()
            })
            .Where(t => t.Interfaces.Count > 0)
            .SelectMany(t => t.Interfaces.Select(i => new { Implementation = t.Type, Interface = i }))
            .ToList();

        foreach (var handler in handlers)
        {
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                handler.Interface,
                handler.Implementation,
                serviceLifetime));
        }
    }

    private static bool IsHandlerInterface(Type type)
    {
        return type == RequestHandlerType ||
               type == StreamHandlerType ||
               type == NotificationHandlerType;
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

        services.TryAdd(ServiceDescriptor.Describe(
            NotificationPublisherType,
            publisherStrategyType,
            serviceLifetime));
    }

    private static void TryAddBehaviors(this IServiceCollection services, MediatorConfiguration configurationOptions)
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

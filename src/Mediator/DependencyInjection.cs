using System.Reflection;
using CQBus.Mediator.Configurations;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.NotificationPublishers;
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
        services.TryAddHandlers(configurationOptions.AssembliesToRegister, configurationOptions.ServiceLifetime);
        services.TryAddPublisher(configurationOptions.PublisherStrategyType, configurationOptions.ServiceLifetime);
        services.TryAddBehaviors(configurationOptions);

        return services;
    }

    private static void TryAddMediator(this IServiceCollection services, MediatorConfiguration configurationOptions)
    {
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IMediator),
            typeof(Mediator),
            configurationOptions.ServiceLifetime));
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(ISender),
            sp => sp.GetRequiredService<IMediator>(),
            configurationOptions.ServiceLifetime));
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IPublisher),
            sp => sp.GetRequiredService<IMediator>(),
            configurationOptions.ServiceLifetime));
    }

    private static void TryAddHandlers(
        this IServiceCollection services,
        List<Assembly> assembliesToRegister,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        if (assembliesToRegister == null || !assembliesToRegister.Any())
        {
            throw new ArgumentNullException(nameof(assembliesToRegister), "At least one assembly must be provided for handler registration.");
        }

        foreach (Type type in assembliesToRegister.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false, IsInterface: false }))
        {
            foreach (Type iType in type.GetInterfaces().Where(i => i.IsGenericType && IsHandlerInterface(i.GetGenericTypeDefinition())))
            {
                services.TryAddEnumerable(ServiceDescriptor.Describe(iType, type, serviceLifetime));
            }
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

using System.Reflection;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CQBus.Mediator.Configurations;

public sealed class MediatorConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; set; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> StreamBehaviorsToRegister { get; set; } = [];
    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Transient;
    public Type PublisherStrategyType { get; set; } = typeof(ForeachAwaitPublisher);

    public MediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);

        return this;
    }

    public MediatorConfiguration AddBehavior(
        Type behaviorType,
        Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        BehaviorsToRegister.Add(new ServiceDescriptor(behaviorType, implementationType, serviceLifetime));

        return this;
    }

    public MediatorConfiguration AddOpenBehavior(
        Type behaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!behaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{behaviorType.Name} must be generic");
        }

        IEnumerable<Type> implementedGenericInterfaces = behaviorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());

        var implementedBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces
            .Where(i => i == typeof(IPipelineBehavior<,>)));

        if (implementedBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{behaviorType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");
        }

        foreach (Type behaviorInterface in implementedBehaviorInterfaces)
        {
            BehaviorsToRegister.Add(ServiceDescriptor.Describe(
                behaviorInterface,
                behaviorType,
                serviceLifetime));
        }

        return this;
    }

    public MediatorConfiguration AddStreamBehavior(
        Type serviceType,
        Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        StreamBehaviorsToRegister.Add(ServiceDescriptor.Describe(
            serviceType,
            implementationType,
            serviceLifetime));

        return this;
    }

    public MediatorConfiguration AddOpenStreamBehavior(
        Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        IEnumerable<Type> implementedGenericInterfaces = openBehaviorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());

        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces
            .Where(i => i == typeof(IStreamPipelineBehavior<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");
        }

        foreach (Type openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            StreamBehaviorsToRegister.Add(ServiceDescriptor.Describe(
                openBehaviorInterface,
                openBehaviorType,
                serviceLifetime));
        }

        return this;
    }
}

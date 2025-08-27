using System.Collections.Frozen;
using System.Reflection;
using CQBus.Mediator.Configurations;
using CQBus.Mediator.Executors;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Invokers;
using CQBus.Mediator.Maps;
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

        services.TryAddMediator(configurationOptions.ServiceLifetime);
        services.TryAddHandlers(configurationOptions.AssembliesToRegister, configurationOptions.ServiceLifetime);
        services.TryAddPublisher(configurationOptions.PublisherStrategyType, configurationOptions.ServiceLifetime);
        services.TryAddBehaviors(configurationOptions);
        services.AddPipelineBuilders(configurationOptions);

        services.RegisterPreCompiledDispatchMaps(configurationOptions.AssembliesToRegister);

        return services;
    }

    private static void TryAddMediator(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IMediator),
            typeof(Mediator),
            serviceLifetime));

        services.TryAdd(ServiceDescriptor.Describe(
            typeof(ISender),
            sp => sp.GetRequiredService<IMediator>(),
            serviceLifetime));

        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IPublisher),
            sp => sp.GetRequiredService<IMediator>(),
            serviceLifetime));
    }

    private static void TryAddHandlers(
        this IServiceCollection services,
        List<Assembly> assembliesToRegister,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        if (assembliesToRegister == null || !assembliesToRegister.Any())
        {
            throw new ArgumentNullException(nameof(assembliesToRegister),
                "At least one assembly must be provided for handler registration.");
        }

        foreach (Type type in assembliesToRegister
                     .SelectMany(a => a.GetTypes())
                     .Distinct()
                     .Where(t => t is { IsClass: true, IsAbstract: false, IsInterface: false }))
        {
            foreach (Type iType in type
                         .GetInterfaces()
                         .Where(i => i.IsGenericType && IsHandlerInterface(i.GetGenericTypeDefinition())))
            {
                Type genericTypeDefinition = iType.GetGenericTypeDefinition();
                var sd = ServiceDescriptor.Describe(iType, type, serviceLifetime);

                if (genericTypeDefinition == NotificationHandlerType)
                {
                    services.TryAddEnumerable(sd);
                    continue;
                }

                services.TryAdd(sd);
            }
        }
    }

    private static bool IsHandlerInterface(Type type) =>
        type == RequestHandlerType ||
        type == StreamHandlerType ||
        type == NotificationHandlerType;

    private static void TryAddPublisher(
        this IServiceCollection services,
        Type publisherStrategyType,
        ServiceLifetime serviceLifetime)
    {
        if (!typeof(INotificationPublisher).IsAssignableFrom(publisherStrategyType))
        {
            throw new InvalidOperationException(
                $"{publisherStrategyType.Name} must implement {nameof(INotificationPublisher)} interface.");
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

    private static void AddPipelineBuilders(this IServiceCollection services, MediatorConfiguration configurationOptions)
    {
        services.Add(ServiceDescriptor.Describe(
            typeof(INotificationExecutor),
            typeof(NotificationExecutor),
            configurationOptions.ServiceLifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IRequestExecutor),
            typeof(RequestExecutor),
            configurationOptions.ServiceLifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IStreamExecutor),
            typeof(StreamExecutor),
            configurationOptions.ServiceLifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IExecutorFactory),
            typeof(ExecutorFactory),
            configurationOptions.ServiceLifetime));
    }

    private static void RegisterPreCompiledDispatchMaps(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies)
    {
        var req = new Dictionary<(Type, Type), Delegate>();
        var notification = new Dictionary<Type, Delegate>();
        var stream = new Dictionary<(Type, Type), Delegate>();

        MethodInfo requestOpen = GetStatic(nameof(MediatorInvoker.Request));
        MethodInfo notificationOpen = GetStatic(nameof(MediatorInvoker.Notification));
        MethodInfo streamOpen = GetStatic(nameof(MediatorInvoker.Stream));

        foreach (Type t in assemblies.SelectMany(SafeGetTypes)
                     .Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (Type it in t.GetInterfaces().Where(i => i.IsGenericType))
            {
                Type open = it.GetGenericTypeDefinition();
                Type[] args = it.GetGenericArguments();

                if (open == RequestHandlerType)
                {
                    (Type tReq, Type tRes) = (args[0], args[1]);
                    Delegate del = requestOpen
                        .MakeGenericMethod(tReq, tRes)
                        .CreateDelegate(typeof(RequestInvoker<>).MakeGenericType(tRes));
                    req[(tReq, tRes)] = del;
                }
                else if (open == NotificationHandlerType)
                {
                    Type tNotification = args[0];
                    notification.TryAdd(
                        tNotification,
                        notificationOpen
                            .MakeGenericMethod(tNotification)
                            .CreateDelegate(typeof(NotificationInvoker<>).MakeGenericType(tNotification))
                    );
                }
                else if (open == StreamHandlerType)
                {
                    (Type tReq, Type tRes) = (args[0], args[1]);
                    Delegate del = streamOpen
                        .MakeGenericMethod(tReq, tRes)
                        .CreateDelegate(typeof(StreamInvoker<>).MakeGenericType(tRes));
                    stream[(tReq, tRes)] = del;
                }
            }
        }

        services.TryAddSingleton<IMediatorDispatchMaps>(new MediatorDispatchMaps(
            req.ToFrozenDictionary(),
            notification.ToFrozenDictionary(),
            stream.ToFrozenDictionary()
        ));

        return;

        static MethodInfo GetStatic(string name) =>
            typeof(MediatorInvoker).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"StaticInvoker.{name} not found.");

        static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try
            {
                return a.DefinedTypes.Select(x => x.AsType());
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x is not null)!;
            }
        }
    }
}

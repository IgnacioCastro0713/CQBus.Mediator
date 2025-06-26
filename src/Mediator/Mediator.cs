using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using CQBus.Mediator.Messages;
using CQBus.Mediator.NotificationPublishers;
using CQBus.Mediator.PipelineBuilders;

namespace CQBus.Mediator;

public sealed class Mediator(
    IServiceProvider serviceProvider,
    INotificationPublisher? publisher = null,
    IRequestPipelineBuilder? requestPipelineBuilder = null,
    INotificationPipelineBuilder? notificationPipelineBuilder = null,
    IStreamPipelineBuilder? streamPipelineBuilder = null) : IMediator
{
    private readonly INotificationPublisher _publisher = publisher ?? new ForeachAwaitPublisher();

    private readonly IRequestPipelineBuilder _requestPipelineBuilder = requestPipelineBuilder ?? new RequestPipelineBuilder();
    private readonly INotificationPipelineBuilder _notificationPipelineBuilder = notificationPipelineBuilder ?? new NotificationPipelineBuilder();
    private readonly IStreamPipelineBuilder _streamPipelineBuilder = streamPipelineBuilder ?? new StreamPipelineBuilder();

    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Delegate> RequestHandlerCache = new();
    private static readonly ConcurrentDictionary<Type, Delegate> NotificationHandlerCache = new();
    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Delegate> StreamHandlerCache = new();

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHandler = (Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken, ValueTask<TResponse>>)
            RequestHandlerCache.GetOrAdd(
                (request.GetType(), typeof(TResponse)),
                types => CreateRequestHandler<TResponse>(types.requestType, types.responseType));

        return requestHandler(_requestPipelineBuilder, request, serviceProvider, cancellationToken);
    }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationHandler = (Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher, CancellationToken, ValueTask>)
            NotificationHandlerCache.GetOrAdd(
                notification.GetType(),
                CreateNotificationHandler<TNotification>);

        return notificationHandler(_notificationPipelineBuilder, notification, serviceProvider, _publisher, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var streamHandler = (Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>>)
            StreamHandlerCache.GetOrAdd(
                (request.GetType(), typeof(TResponse)),
                types => CreateStreamHandler<TResponse>(types.requestType, types.responseType));

        return streamHandler(_streamPipelineBuilder, request, serviceProvider, cancellationToken);
    }

    private static Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken, ValueTask<TResponse>>
        CreateRequestHandler<TResponse>(Type requestType, Type responseType)
    {
        ParameterExpression pipelineBuilderParam = Expression.Parameter(typeof(IRequestPipelineBuilder), "pipelineBuilder");
        ParameterExpression requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        UnaryExpression requestCast = Expression.Convert(requestParam, requestType);

        MethodInfo buildAndExecuteMethod = typeof(IRequestPipelineBuilder)
            .GetMethods()
            .Single(m => m.Name == nameof(IRequestPipelineBuilder.BuildAndExecute));

        MethodInfo genericMethod = buildAndExecuteMethod.MakeGenericMethod(requestType, responseType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            requestCast,
            serviceProviderParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken, ValueTask<TResponse>>>(
            methodCall,
            pipelineBuilderParam,
            requestParam,
            serviceProviderParam,
            cancellationTokenParam).Compile();
    }

    private static Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher, CancellationToken, ValueTask>
        CreateNotificationHandler<TNotification>(Type notificationType)
        where TNotification : INotification
    {
        ParameterExpression pipelineBuilderParam = Expression.Parameter(typeof(INotificationPipelineBuilder), "pipelineBuilder");
        ParameterExpression notificationParam = Expression.Parameter(typeof(TNotification), "notification");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression publisherParam = Expression.Parameter(typeof(INotificationPublisher), "publisher");
        ParameterExpression cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        MethodInfo buildAndExecuteMethod = typeof(INotificationPipelineBuilder)
            .GetMethods()
            .Single(m => m.Name == nameof(INotificationPipelineBuilder.BuildAndExecute));

        MethodInfo genericMethod = buildAndExecuteMethod.MakeGenericMethod(notificationType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            notificationParam,
            serviceProviderParam,
            publisherParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher, CancellationToken, ValueTask>>(
            methodCall,
            pipelineBuilderParam,
            notificationParam,
            serviceProviderParam,
            publisherParam,
            cancellationTokenParam).Compile();
    }

    private static Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>>
        CreateStreamHandler<TResponse>(Type requestType, Type responseType)
    {
        ParameterExpression pipelineBuilderParam = Expression.Parameter(typeof(IStreamPipelineBuilder), "pipelineBuilder");
        ParameterExpression requestParam = Expression.Parameter(typeof(IStreamRequest<TResponse>), "request");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        UnaryExpression requestCast = Expression.Convert(requestParam, requestType);

        MethodInfo buildAndExecuteMethod = typeof(IStreamPipelineBuilder)
            .GetMethods()
            .Single(m => m.Name == nameof(IStreamPipelineBuilder.BuildAndExecute));

        MethodInfo genericMethod = buildAndExecuteMethod.MakeGenericMethod(requestType, responseType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            requestCast,
            serviceProviderParam,
            cancellationTokenParam);

        return Expression.Lambda<Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>>>(
            methodCall,
            pipelineBuilderParam,
            requestParam,
            serviceProviderParam,
            cancellationTokenParam).Compile();
    }
}

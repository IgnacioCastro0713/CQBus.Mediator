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

    private readonly IRequestPipelineBuilder _requestPipelineBuilder =
        requestPipelineBuilder ?? new RequestPipelineBuilder();

    private readonly INotificationPipelineBuilder _notificationPipelineBuilder =
        notificationPipelineBuilder ?? new NotificationPipelineBuilder();

    private readonly IStreamPipelineBuilder _streamPipelineBuilder =
        streamPipelineBuilder ?? new StreamPipelineBuilder();

    private static readonly MethodInfo BuildAndExecuteRequestMethod =
        typeof(IRequestPipelineBuilder).GetMethod(nameof(IRequestPipelineBuilder.BuildAndExecute))!;

    private static readonly MethodInfo BuildAndExecuteNotificationMethod =
        typeof(INotificationPipelineBuilder).GetMethod(nameof(INotificationPipelineBuilder.BuildAndExecute))!;

    private static readonly MethodInfo BuildAndExecuteStreamMethod =
        typeof(IStreamPipelineBuilder).GetMethod(nameof(IStreamPipelineBuilder.BuildAndExecute))!;

    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Delegate> RequestHandlerCache =
        new(new TypeTuple());

    private static readonly ConcurrentDictionary<Type, Delegate> NotificationHandlerCache = new();

    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Delegate> StreamHandlerCache =
        new(new TypeTuple());

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        Type responseType = typeof(TResponse);

        var requestHandler =
            (Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken,
                ValueTask<TResponse>>)
            RequestHandlerCache.GetOrAdd(
                (requestType, responseType),
                static types => CreateRequestHandler<TResponse>(types.requestType, types.responseType));

        return requestHandler(_requestPipelineBuilder, request, serviceProvider, cancellationToken);
    }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        Type notificationType = notification.GetType();

        var notificationHandler =
            (Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher,
                CancellationToken, ValueTask>)
            NotificationHandlerCache.GetOrAdd(
                notificationType,
                static type => CreateNotificationHandler<TNotification>(type));

        return notificationHandler(_notificationPipelineBuilder, notification, serviceProvider, _publisher,
            cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        Type responseType = typeof(TResponse);

        var streamHandler =
            (Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken,
                IAsyncEnumerable<TResponse>>)
            StreamHandlerCache.GetOrAdd(
                (requestType, responseType),
                static types => CreateStreamHandler<TResponse>(types.requestType, types.responseType));

        return streamHandler(_streamPipelineBuilder, request, serviceProvider, cancellationToken);
    }

    private static Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken,
            ValueTask<TResponse>>
        CreateRequestHandler<TResponse>(Type requestType, Type responseType)
    {
        ParameterExpression pipelineBuilderParam =
            Expression.Parameter(typeof(IRequestPipelineBuilder), "pipelineBuilder");
        ParameterExpression requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression cancellationTokenParam =
            Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        UnaryExpression requestCast = Expression.Convert(requestParam, requestType);
        MethodInfo genericMethod = BuildAndExecuteRequestMethod.MakeGenericMethod(requestType, responseType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            requestCast,
            serviceProviderParam,
            cancellationTokenParam);

        return Expression
            .Lambda<Func<IRequestPipelineBuilder, IRequest<TResponse>, IServiceProvider, CancellationToken,
                ValueTask<TResponse>>>(
                methodCall,
                pipelineBuilderParam,
                requestParam,
                serviceProviderParam,
                cancellationTokenParam).Compile();
    }

    private static Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher,
            CancellationToken, ValueTask>
        CreateNotificationHandler<TNotification>(Type notificationType)
        where TNotification : INotification
    {
        ParameterExpression pipelineBuilderParam =
            Expression.Parameter(typeof(INotificationPipelineBuilder), "pipelineBuilder");
        ParameterExpression notificationParam = Expression.Parameter(typeof(TNotification), "notification");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression publisherParam = Expression.Parameter(typeof(INotificationPublisher), "publisher");
        ParameterExpression cancellationTokenParam =
            Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        MethodInfo genericMethod = BuildAndExecuteNotificationMethod.MakeGenericMethod(notificationType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            notificationParam,
            serviceProviderParam,
            publisherParam,
            cancellationTokenParam);

        return Expression
            .Lambda<Func<INotificationPipelineBuilder, TNotification, IServiceProvider, INotificationPublisher,
                CancellationToken, ValueTask>>(
                methodCall,
                pipelineBuilderParam,
                notificationParam,
                serviceProviderParam,
                publisherParam,
                cancellationTokenParam).Compile();
    }

    private static Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken,
            IAsyncEnumerable<TResponse>>
        CreateStreamHandler<TResponse>(Type requestType, Type responseType)
    {
        ParameterExpression pipelineBuilderParam =
            Expression.Parameter(typeof(IStreamPipelineBuilder), "pipelineBuilder");
        ParameterExpression requestParam = Expression.Parameter(typeof(IStreamRequest<TResponse>), "request");
        ParameterExpression serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression cancellationTokenParam =
            Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        UnaryExpression requestCast = Expression.Convert(requestParam, requestType);
        MethodInfo genericMethod = BuildAndExecuteStreamMethod.MakeGenericMethod(requestType, responseType);

        MethodCallExpression methodCall = Expression.Call(
            pipelineBuilderParam,
            genericMethod,
            requestCast,
            serviceProviderParam,
            cancellationTokenParam);

        return Expression
            .Lambda<Func<IStreamPipelineBuilder, IStreamRequest<TResponse>, IServiceProvider, CancellationToken,
                IAsyncEnumerable<TResponse>>>(
                methodCall,
                pipelineBuilderParam,
                requestParam,
                serviceProviderParam,
                cancellationTokenParam).Compile();
    }
}

internal class TypeTuple : IEqualityComparer<(Type, Type)>
{
    public bool Equals((Type, Type) x, (Type, Type) y) => x.Item1 == y.Item1 && x.Item2 == y.Item2;

    public int GetHashCode((Type, Type) obj) => HashCode.Combine(obj.Item1, obj.Item2);
}

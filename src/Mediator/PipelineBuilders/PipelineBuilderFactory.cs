namespace CQBus.Mediator.PipelineBuilders;

public interface IPipelineBuilderFactory
{
    IRequestPipelineBuilder Request { get; }
    INotificationPipelineBuilder Notification { get; }
    IStreamPipelineBuilder Stream { get; }
}

internal sealed class PipelineBuilderFactory : IPipelineBuilderFactory
{
    public PipelineBuilderFactory(
        IRequestPipelineBuilder request,
        INotificationPipelineBuilder notification,
        IStreamPipelineBuilder stream)
    {
        Request = request;
        Notification = notification;
        Stream = stream;
    }

    public IRequestPipelineBuilder Request { get; }
    public INotificationPipelineBuilder Notification { get; }
    public IStreamPipelineBuilder Stream { get; }
}

using CQBus.Mediator.Handlers;

namespace API.Features.AddWeather;

public sealed class AddWeatherDomainEventHandler(ILogger<AddWeatherDomainEventHandler> logger)
    : INotificationHandler<AddWeatherDomainEvent>
{
    public ValueTask Handle(AddWeatherDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed Event {NewWeather}", notification.NewWeather);
        return ValueTask.CompletedTask;
    }
}
public sealed class AddWeatherDomainEventHandler2(ILogger<AddWeatherDomainEventHandler> logger)
    : INotificationHandler<AddWeatherDomainEvent>
{
    public ValueTask Handle(AddWeatherDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed Event {NewWeather}", notification.NewWeather);
        return ValueTask.CompletedTask;
    }
}

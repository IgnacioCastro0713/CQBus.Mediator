using CQBus.Mediator.Messages;

namespace API.Features.AddWeather;

public sealed record AddWeatherDomainEvent(string NewWeather) : INotification;

using CQBus.Mediator.Messages;

namespace API.Features.UpdateWeather;

public sealed record UpdateWeatherStreamRequest(string Name) : IStreamRequest<WeatherForecast>;

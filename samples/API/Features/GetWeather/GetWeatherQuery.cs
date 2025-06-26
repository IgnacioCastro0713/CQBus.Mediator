using CQBus.Mediator.Messages;

namespace API.Features.GetWeather;

public sealed record GetWeatherQuery : IRequest<IEnumerable<WeatherForecast>>;

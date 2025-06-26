using CQBus.Mediator.Messages;

namespace API.Features.GetWeatherByName;

public sealed record GetWeatherByContainsNameQuery(string Name) : IRequest<IEnumerable<WeatherForecast>>;

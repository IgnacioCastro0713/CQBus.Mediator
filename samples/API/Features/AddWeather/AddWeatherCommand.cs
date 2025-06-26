using CQBus.Mediator.Messages;

namespace API.Features.AddWeather;

public sealed record AddWeatherCommand(string Name) : IRequest<Unit>;

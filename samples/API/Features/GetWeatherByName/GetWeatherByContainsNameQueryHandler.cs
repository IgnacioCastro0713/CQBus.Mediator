using CQBus.Mediator.Handlers;

namespace API.Features.GetWeatherByName;

public sealed class GetWeatherByContainsNameQueryHandler(IWeatherService weatherService)
    : IRequestHandler<GetWeatherByContainsNameQuery, IEnumerable<WeatherForecast>>
{
    public async ValueTask<IEnumerable<WeatherForecast>> Handle(
        GetWeatherByContainsNameQuery request,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<WeatherForecast> weatherForecasts = weatherService.GetWeatherForecastByContainsName(request.Name);

        return await Task.FromResult(weatherForecasts);
    }
}

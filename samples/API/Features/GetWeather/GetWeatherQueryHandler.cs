using CQBus.Mediator.Handlers;

namespace API.Features.GetWeather;

public sealed class GetWeatherQueryHandler(IWeatherService weatherService)
    : IRequestHandler<GetWeatherQuery, IEnumerable<WeatherForecast>>
{
    public async ValueTask<IEnumerable<WeatherForecast>> Handle(
        GetWeatherQuery request,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<WeatherForecast> weatherForecasts = weatherService.GetWeatherForecasts();

        return await Task.FromResult(weatherForecasts);
    }
}

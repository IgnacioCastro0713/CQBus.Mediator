using System.Runtime.CompilerServices;
using CQBus.Mediator.Handlers;

namespace API.Features.UpdateWeather;

public sealed class UpdateWeatherStreamRequestHandler(IWeatherService service) :
    IStreamRequestHandler<UpdateWeatherStreamRequest, WeatherForecast>
{
    public async IAsyncEnumerable<WeatherForecast> Handle(
        UpdateWeatherStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IEnumerable<WeatherForecast> weatherForecasts = service.GetWeatherForecastByContainsName(request.Name);

        foreach (WeatherForecast weatherForecast in weatherForecasts)
        {
            await Task.Delay(1000, cancellationToken);

            yield return weatherForecast;
        }
    }
}

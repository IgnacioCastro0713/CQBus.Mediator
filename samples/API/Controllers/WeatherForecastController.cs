using API.Features.AddWeather;
using API.Features.GetWeather;
using API.Features.GetWeatherByName;
using API.Features.UpdateWeather;
using CQBus.Mediator;
using CQBus.Mediator.Messages;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(ISender sender)
    : ControllerBase
{
    [HttpGet("GetWeatherForecast")]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        var query = new GetWeatherQuery();

        IEnumerable<WeatherForecast> values = await sender.Send(query);

        return values;
    }

    [HttpGet("GetWeatherForecastByContainsName")]
    public async Task<IEnumerable<WeatherForecast>> ByContainsName([FromQuery] string name)
    {
        var query = new GetWeatherByContainsNameQuery(name);

        IEnumerable<WeatherForecast> values = await sender.Send(query);

        return values;
    }

    [HttpPost("AddWeatherForecast")]
    public async Task<Unit> Post([FromQuery] string name)
    {
        var command = new AddWeatherCommand(name);

        Unit result = await sender.Send(command);

        return result;
    }

    [HttpGet("GetUpdateWeatherForecast")]
    public IAsyncEnumerable<WeatherForecast> GetUpdateWeatherForecast([FromQuery] string name)
    {
        var streamRequest = new UpdateWeatherStreamRequest(name);

        IAsyncEnumerable<WeatherForecast> result = sender.CreateStream(streamRequest);

        return result;
    }
}

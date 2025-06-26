namespace API;

public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}

public sealed class WeatherService() : IWeatherService
{
    private static string[] _summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public IEnumerable<WeatherForecast> GetWeatherForecasts()
    {
        return _summaries.Select((s, index) => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = s
        }).ToArray();
    }

    public IEnumerable<WeatherForecast> GetWeatherForecastByContainsName(string name)
    {
        return _summaries.Where(s => s.Contains(name)).Select((s, index) => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = s
        });
    }

    public void AddWeatherForecast(string name)
    {
        var current = _summaries.ToList();
        current.Add(name);
        _summaries = current.ToArray();
    }
};

public interface IWeatherService
{
    IEnumerable<WeatherForecast> GetWeatherForecasts();
    IEnumerable<WeatherForecast> GetWeatherForecastByContainsName(string name);
    void AddWeatherForecast(string name);
}

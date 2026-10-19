namespace kuv_api;

/// <summary>
/// Sample weather forecast returned by the template endpoint.
/// </summary>
public class WeatherForecast
{
    /// <summary>
    /// Date of the forecast.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Temperature in Celsius.
    /// </summary>
    public int TemperatureC { get; set; }

    /// <summary>
    /// Temperature in Fahrenheit, derived from Celsius.
    /// </summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    /// <summary>
    /// Short description of the weather.
    /// </summary>
    public string? Summary { get; set; }
}

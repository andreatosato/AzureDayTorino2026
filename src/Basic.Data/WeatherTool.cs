using System.ComponentModel;

namespace Basic.Data;

public static class WeatherTool
{
    [Description("Leggi il meteo sintetico di Torino: condizioni e temperatura in gradi Celsius.")]
    public static Weather GetWeather(string city)
    {
        if (!string.Equals(city.Trim(), "Torino", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Citta' non supportata. Il mock contiene solo Torino.", nameof(city));
        }

        return new("Torino", "pioggia", 14);
    }
}

public sealed record Weather(string City, string Conditions, int TemperatureC);

public static class BasicFacts
{
    public const string Context =
        "Rispondi in italiano, brevemente. Sono dati sintetici di una demo. " +
        "Usa solo i fatti forniti o letti dai tool; non inventare dati. ";
}

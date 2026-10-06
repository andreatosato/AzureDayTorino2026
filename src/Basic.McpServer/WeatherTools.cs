using System.ComponentModel;
using Basic.Data;
using ModelContextProtocol.Server;

namespace Basic.McpServer;

[McpServerToolType]
public sealed class WeatherTools
{
    [McpServerTool(Name = "get_weather"), Description("Leggi il meteo sintetico di Torino.")]
    public Weather GetWeather(string city) => WeatherTool.GetWeather(city);
}

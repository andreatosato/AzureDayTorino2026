using System.ComponentModel;
using Basic.Data;

namespace Basic.Skill;

public static class CityBriefingTools
{
    [Description("Leggi la lista sintetica di cose da portare per visitare Torino con pioggia e 14 C.")]
    public static PackingList GetPackingList(string city)
    {
        var weather = WeatherTool.GetWeather(city);
        return new(weather.City, ["ombrello", "giacca impermeabile"]);
    }

    [Description("Leggi le precauzioni sintetiche per visitare Torino con la pioggia. Non sono allerte ufficiali.")]
    public static TravelAlerts GetTravelAlerts(string city)
    {
        var weather = WeatherTool.GetWeather(city);
        return new(weather.City, ["Attenzione ai marciapiedi bagnati: possono essere scivolosi."]);
    }
}

public sealed record PackingList(string City, string[] Items);
public sealed record TravelAlerts(string City, string[] Precautions);

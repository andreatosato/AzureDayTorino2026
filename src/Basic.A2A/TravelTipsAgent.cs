using Basic.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Basic.A2A;

public static class TravelTipsAgent
{
    public static AIAgent Create(IChatClient model) => new ChatClientAgent(model,
        name: "TravelTipsAgent",
        instructions: BasicFacts.Context +
            "Leggi il meteo con get_weather, poi dai esattamente due consigli pratici per visitare la citta'.",
        tools: [AIFunctionFactory.Create(WeatherTool.GetWeather, "get_weather")])
        .AsBuilder().UseOpenTelemetry(configure: telemetry => telemetry.EnableSensitiveData = true).Build();
}

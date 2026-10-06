using Basic.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Basic.Skill;

public static class SkillDemo
{
    public static async Task<string> RunAsync(IChatClient model, string prompt, CancellationToken ct)
    {
        var skill = new AgentSkillsProvider(Path.Combine(AppContext.BaseDirectory, "skills", "base"),
            options: new() { DisableLoadSkillApproval = true });
        var agent = new ChatClientAgent(model, new ChatClientAgentOptions
        {
            Name = "BriefingSkill",
            ChatOptions = new()
            {
                Instructions = BasicFacts.Context + "Carica la skill pertinente prima di rispondere.",
                Tools =
                [
                    AIFunctionFactory.Create(WeatherTool.GetWeather, "get_weather"),
                    AIFunctionFactory.Create(CityBriefingTools.GetPackingList, "get_packing_list"),
                    AIFunctionFactory.Create(CityBriefingTools.GetTravelAlerts, "get_travel_alerts")
                ]
            },
            AIContextProviders = [skill]
        });

        return (await agent.RunAsync(prompt, cancellationToken: ct)).Text;
    }
}

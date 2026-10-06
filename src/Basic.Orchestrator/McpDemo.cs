using Basic.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Basic.Orchestrator;

public static class McpDemo
{
    public static async Task<string> RunAsync(IChatClient model, HttpClient http, string prompt, CancellationToken ct)
    {
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new() { Endpoint = new Uri("http://mcp-server/mcp") }, http), cancellationToken: ct);
        var tools = await mcp.ListToolsAsync(cancellationToken: ct);
        var agent = new ChatClientAgent(model, name: "MeteoMcp",
            instructions: BasicFacts.Context + "Per il meteo usa get_weather.",
            tools: [.. tools.Where(tool => tool.Name == "get_weather")]);

        return (await agent.RunAsync(prompt, cancellationToken: ct)).Text;
    }
}

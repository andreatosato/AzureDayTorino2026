using Advance.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Advance.A2A;

public static class LogisticsAgent
{
    public static AIAgent Create(IChatClient model, IHttpClientFactory clients) => new ChatClientAgent(model,
        name: "LogisticsAgent",
        instructions: Scenario.Context + Scenario.DeliveryProcedure,
        tools:
        [
            AIFunctionFactory.Create((string orderId, CancellationToken ct) =>
                CallMcpAsync(clients, "get_order", orderId, ct),
                "get_order", "Legge l'ordine dal server MCP."),
            AIFunctionFactory.Create((string orderId, CancellationToken ct) =>
                CallMcpAsync(clients, "get_delivery_options", orderId, ct),
                "get_delivery_options", "Legge le alternative di consegna dal server MCP.")
        ])
        .AsBuilder().UseOpenTelemetry(configure: telemetry => telemetry.EnableSensitiveData = true).Build();

    private static async Task<string> CallMcpAsync(IHttpClientFactory clients, string tool, string orderId, CancellationToken ct)
    {
        using var http = clients.CreateClient("mcp");
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new() { Endpoint = new Uri("http://mcp-server/mcp") }, http), cancellationToken: ct);
        var result = await mcp.CallToolAsync(tool, new Dictionary<string, object?> { ["orderId"] = orderId },
            cancellationToken: ct);
        if (result.IsError == true)
            throw new InvalidOperationException($"Il tool MCP {tool} ha restituito un errore.");
        return System.Text.Json.JsonSerializer.Serialize(result.Content);
    }
}

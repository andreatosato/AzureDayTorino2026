using Basic.Orchestrator;
using Shared.ServiceDefaults;

namespace Basic.Web;

/// <summary>Risposta BASIC con misure Web e dati dello specialista A2A, se invocato.</summary>
public sealed record BasicRunResponse(string Text, string TraceId, double DurationMs, ExecutionStep[] Steps,
    long? InputTokens, long? OutputTokens, RemoteAgentDetails? RemoteAgent = null)
{
    public long? TotalInputTokens => RemoteAgent is null ? InputTokens :
        AddTokens(InputTokens, RemoteAgent.Execution?.InputTokens);

    public long? TotalOutputTokens => RemoteAgent is null ? OutputTokens :
        AddTokens(OutputTokens, RemoteAgent.Execution?.OutputTokens);

    private static long? AddTokens(long? local, long? remote) =>
        local is { } input && remote is { } output ? checked(input + output) : null;
}

using System.Diagnostics;
using Microsoft.Agents.AI;

namespace Shared.ServiceDefaults;

public static class DemoTelemetry
{
    public const string SourceName = "Demo.Execution";
#pragma warning disable MAAI001 // The SDK's telemetry source name is experimental.
    public static string AgentSourceName { get; } = OpenTelemetryAgent.DefaultSourceName;
#pragma warning restore MAAI001
    public static readonly ActivitySource Source = new(SourceName);

    public static async Task<T> TraceAsync<T>(string operation, Func<Task<T>> action)
    {
        using var activity = Source.StartActivity(operation);
        try
        {
            return await action();
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            throw;
        }
    }
}

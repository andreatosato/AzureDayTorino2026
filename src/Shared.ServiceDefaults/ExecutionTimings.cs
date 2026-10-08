using System.Diagnostics;
using System.Globalization;

namespace Shared.ServiceDefaults;

public sealed class ExecutionTimings : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly Activity _root;
    private readonly List<ExecutionStep> _steps = [];

    public ExecutionTimings(string technology, string operation = "basic.run")
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is DemoTelemetry.SourceName ||
                source.Name == DemoTelemetry.AgentSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = Record
        };
        ActivitySource.AddActivityListener(_listener);
        _root = DemoTelemetry.Source.StartActivity(operation) ??
            throw new InvalidOperationException("Impossibile creare la traccia BASIC.");
        _root.SetTag("demo.technology", technology);
    }

    public ExecutionReport Complete(string text)
    {
        _root.Stop();
        lock (_steps)
        {
            var steps = _steps.OrderBy(step => step.StartMs).ToArray();
            return new(text, _root.TraceId.ToString(), _root.Duration.TotalMilliseconds,
                steps, SumTokens(steps, step => step.InputTokens), SumTokens(steps, step => step.OutputTokens));
        }
    }

    public void Fail(Exception exception)
    {
        _root.SetStatus(ActivityStatusCode.Error);
        _root.SetTag("error.type", exception.GetType().FullName);
    }

    private void Record(Activity activity)
    {
        // Only descendants of this request, never other requests or remote server spans.
        var parent = activity.Parent;
        while (parent is not null && !ReferenceEquals(parent, _root))
            parent = parent.Parent;
        if (parent is null)
            return;

        var operation = activity.GetTagItem("gen_ai.operation.name")?.ToString() ?? "stage";
        var step = new ExecutionStep(activity.DisplayName, operation,
            (activity.StartTimeUtc - _root.StartTimeUtc).TotalMilliseconds,
            activity.Duration.TotalMilliseconds, activity.SpanId.ToString(),
            activity.ParentSpanId.ToString(), activity.Status == ActivityStatusCode.Error,
            operation == "chat" ? ReadTokens(activity, "gen_ai.usage.input_tokens") : null,
            operation == "chat" ? ReadTokens(activity, "gen_ai.usage.output_tokens") : null,
            activity.GetTagItem("gen_ai.agent.name")?.ToString());
        lock (_steps)
            _steps.Add(step);
    }

    private static long? ReadTokens(Activity activity, string tag) =>
        activity.GetTagItem(tag) is { } value ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : null;

    private static long? SumTokens(ExecutionStep[] steps, Func<ExecutionStep, long?> getTokens)
    {
        var calls = steps.Where(step => step.Operation == "chat").ToArray();
        if (calls.Length == 0)
            return null;
        long total = 0;
        foreach (var call in calls)
        {
            if (getTokens(call) is not { } count)
                return null;
            total = checked(total + count);
        }
        return total;
    }

    public void Dispose()
    {
        _root.Dispose();
        _listener.Dispose();
    }
}

/// <summary>Misure di una singola esecuzione nel processo che la ospita.</summary>
public sealed record ExecutionReport(string Text, string TraceId, double DurationMs, ExecutionStep[] Steps,
    long? InputTokens, long? OutputTokens);

/// <summary>Operazione misurata senza prompt, argomenti o risultati dei tool.</summary>
public sealed record ExecutionStep(string Name, string Operation, double StartMs, double DurationMs,
    string SpanId, string ParentSpanId, bool Failed, long? InputTokens, long? OutputTokens,
    string? AgentName = null);

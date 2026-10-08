using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.ServiceDefaults;

public static class RemoteExecutionTelemetry
{
    public const string HeaderName = "Demo-Agent-Metrics";
    public const string ActivityProperty = "demo.remote.execution";

    public static WebApplication UseRemoteExecutionTelemetry(this WebApplication app, string path)
    {
        app.Use(async (context, next) =>
        {
            // Streaming sends headers before the agent finishes; only blocking calls can carry final metrics.
            if (!HttpMethods.IsPost(context.Request.Method) ||
                !context.Request.Path.StartsWithSegments(path) ||
                context.Request.Path.Value?.EndsWith("/message:send", StringComparison.Ordinal) != true)
            {
                await next(context);
                return;
            }

            using var timings = new ExecutionTimings("a2a", "a2a.run");
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode < 400)
                    context.Response.Headers[HeaderName] =
                        JsonSerializer.Serialize(timings.Complete(""), JsonSerializerOptions.Web);
                return Task.CompletedTask;
            });
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                timings.Fail(exception);
                throw;
            }
        });
        return app;
    }
}

public sealed class RemoteExecutionHandler(ILogger<RemoteExecutionHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var stage = Activity.Current;
        while (stage is not null && stage.DisplayName != "a2a.remote_agent")
            stage = stage.Parent;
        if (stage is null || !response.IsSuccessStatusCode)
            return response;

        try
        {
            if (!response.Headers.TryGetValues(RemoteExecutionTelemetry.HeaderName, out var values))
            {
                logger.LogWarning("Lo specialista A2A non ha restituito le metriche della demo.");
                return response;
            }
            var execution = JsonSerializer.Deserialize<ExecutionReport>(values.Single(), JsonSerializerOptions.Web)
                ?? throw new JsonException("Metriche A2A mancanti.");
            if (execution.TraceId != stage.TraceId.ToString())
                throw new InvalidOperationException("Le metriche A2A appartengono a una traccia diversa.");
            stage.SetCustomProperty(RemoteExecutionTelemetry.ActivityProperty, execution);
            return response;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            logger.LogError(exception, "Metriche A2A non valide.");
            response.Dispose();
            throw;
        }
    }
}

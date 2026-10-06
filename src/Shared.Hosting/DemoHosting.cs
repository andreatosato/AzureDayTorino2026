using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Shared.Hosting;

public static class DemoHosting
{
    public static void AddDemoServices(this WebApplicationBuilder builder, bool withModel = true)
    {
        builder.AddServiceDefaults();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<DemoExceptionHandler>();
        builder.Services.AddRequestTimeouts(options =>
            options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromMinutes(2) });

        if (withModel)
        {
            builder.Services.AddSingleton<IChatClient>(_ =>
            {
                string Required(string name) => builder.Configuration[$"Model:{name}"] is { Length: > 0 } value
                    ? value
                    : throw new ModelConfigurationException();

                var endpoint = new Uri(Required("Endpoint"));
                var model = Required("Name");
                if (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback))
                {
                    throw new ModelConfigurationException();
                }

                var options = new OpenAIClientOptions
                {
                    Endpoint = endpoint,
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                    NetworkTimeout = TimeSpan.FromSeconds(90)
                };
                var authentication = builder.Configuration["Model:Authentication"] ?? "ApiKey";
#pragma warning disable OPENAI001 // The SDK's Entra authentication constructor is experimental.
                var client = authentication switch
                {
                    "AzureCli" when endpoint.Scheme == "https" => new OpenAIClient(
                        new BearerTokenPolicy(new AzureCliCredential(new AzureCliCredentialOptions
                        {
                            TenantId = Required("TenantId")
                        }), "https://cognitiveservices.azure.com/.default"), options),
                    "ApiKey" => new OpenAIClient(new ApiKeyCredential(Required("ApiKey")), options),
                    _ => throw new ModelConfigurationException()
                };
#pragma warning restore OPENAI001
                IChatClient chatClient = client.GetChatClient(model).AsIChatClient();
                if (builder.Configuration.GetValue<ReasoningEffort?>("Model:ReasoningEffort") is { } effort)
                {
                    chatClient = chatClient.AsBuilder().ConfigureOptions(options =>
                        options.Reasoning = new ReasoningOptions { Effort = effort }).Build();
                }
                return chatClient;
            });
        }
    }

    public static void UseDemoDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseRequestTimeouts();
        app.MapDefaultEndpoints();
    }
}

public sealed class ModelConfigurationException : InvalidOperationException
{
    public ModelConfigurationException() : base(
        "Configura Model:Endpoint (URL API v1) e Model:Name nell'AppHost. " +
        "Usa Model:Authentication=AzureCli con Model:TenantId oppure ApiKey con Model:ApiKey. " +
        "Usa HTTPS oppure HTTP solo su loopback. Nessun modello fake viene usato.") { }
}

internal sealed class DemoExceptionHandler(ILogger<DemoExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Demo fallita. TraceId: {TraceId}", context.TraceIdentifier);
        var missingModel = exception is ModelConfigurationException;
        context.Response.StatusCode = missingModel ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status502BadGateway;
        await Results.Problem(
            statusCode: context.Response.StatusCode,
            title: missingModel ? "Modello non configurato" : "Errore durante la demo",
            detail: missingModel ? exception.Message : "Una dipendenza o l'agente ha restituito un errore. Consultare i log in Aspire.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}

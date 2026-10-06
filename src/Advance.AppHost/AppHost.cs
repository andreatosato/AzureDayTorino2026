var builder = DistributedApplication.CreateBuilder(args);
string Required(string name) => builder.Configuration[$"Model:{name}"] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException(
        $"Manca Model:{name}. Configura Model:Endpoint, Model:Name e l'autenticazione " +
        "con dotnet user-secrets --project src\\Advance.AppHost set oppure Model__* nell'ambiente. Vedi README.md.");
var endpoint = Required("Endpoint");
var model = Required("Name");
var authentication = builder.Configuration["Model:Authentication"] ?? "ApiKey";
if (authentication is not ("ApiKey" or "AzureCli"))
    throw new InvalidOperationException("Model:Authentication deve essere ApiKey oppure AzureCli.");
var tenantId = authentication == "AzureCli" ? Required("TenantId") : null;
var key = authentication == "ApiKey" ? Required("ApiKey") : null;
var modelKey = key is not null
    ? builder.AddParameter("model-api-key", () => key,
        secret: true, publishValueAsDefault: false)
    : null;

var commerce = builder.AddProject<Projects.Advance_MockApi>("commerce", launchProfileName: "http")
    .WithHttpHealthCheck("/health");
var mcpServer = builder.AddProject<Projects.Advance_McpServer>("mcp-server", launchProfileName: "http")
    .WithReference(commerce).WaitFor(commerce)
    .WithHttpHealthCheck("/health");
var specialists = builder.AddProject<Projects.Advance_A2A>("specialists", launchProfileName: "http")
    .WithReference(mcpServer).WaitFor(mcpServer)
    .WithHttpHealthCheck("/health");

var advance = builder.AddProject<Projects.Advance_Commerce>("advance", launchProfileName: "http")
    .WithReference(specialists).WaitFor(specialists);

foreach (var project in new[] { specialists, advance })
{
    project.WithEnvironment("Model__Endpoint", endpoint)
        .WithEnvironment("Model__Name", model)
        .WithEnvironment("Model__Authentication", authentication);
    if (modelKey is not null)
        project.WithEnvironment("Model__ApiKey", modelKey);
    if (tenantId is not null)
        project.WithEnvironment("Model__TenantId", tenantId);
    if (builder.Configuration["Model:ReasoningEffort"] is { Length: > 0 } effort)
        project.WithEnvironment("Model__ReasoningEffort", effort);
}

builder.Build().Run();

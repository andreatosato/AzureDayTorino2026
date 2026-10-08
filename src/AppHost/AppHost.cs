var builder = DistributedApplication.CreateBuilder(args);
string Required(string name) => builder.Configuration[$"Model:{name}"] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException(
        $"Manca Model:{name}. Configura Model:Endpoint, Model:Name e l'autenticazione " +
        "con dotnet user-secrets --project src\\AppHost set oppure Model__* nell'ambiente. Vedi README.md.");
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

var basicMcp = builder.AddProject<Projects.Basic_McpServer>("basic-mcp-server", launchProfileName: "http")
    .WithHttpHealthCheck("/health")
    .WithExplicitStart();
var basicSpecialist = builder.AddProject<Projects.Basic_A2A>("basic-a2a", launchProfileName: "http")
    .WithHttpHealthCheck("/health")
    .WithExplicitStart();

var basic = builder.AddProject<Projects.Basic_Web>("basic-web", launchProfileName: "http")
    .WithReference(basicMcp, "mcp-server").WaitFor(basicMcp)
    .WithReference(basicSpecialist, "specialists").WaitFor(basicSpecialist)
    .WithExplicitStart();

var commerce = builder.AddProject<Projects.Advance_MockApi>("advance-mock-api", launchProfileName: "http")
    .WithHttpHealthCheck("/health")
    .WithExplicitStart();
var advanceMcp = builder.AddProject<Projects.Advance_McpServer>("advance-mcp-server", launchProfileName: "http")
    .WithReference(commerce, "commerce").WaitFor(commerce)
    .WithHttpHealthCheck("/health")
    .WithExplicitStart();
var advanceSpecialist = builder.AddProject<Projects.Advance_A2A>("advance-a2a", launchProfileName: "http")
    .WithReference(advanceMcp, "mcp-server").WaitFor(advanceMcp)
    .WithHttpHealthCheck("/health")
    .WithExplicitStart();
var advance = builder.AddProject<Projects.Advance_Web>("advance-web", launchProfileName: "http")
    .WithReference(advanceSpecialist, "specialists").WaitFor(advanceSpecialist)
    .WithExplicitStart();

foreach (var project in new[] { basicSpecialist, basic, advanceSpecialist, advance })
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

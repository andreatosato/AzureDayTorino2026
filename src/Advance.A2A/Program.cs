using A2A;
using Shared.Hosting;
using Advance.A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices();
builder.Services.AddHttpClient("mcp", http => http.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddKeyedSingleton<AIAgent>("logistics", (services, _) =>
    LogisticsAgent.Create(services.GetRequiredService<IChatClient>(), services.GetRequiredService<IHttpClientFactory>()));
builder.AddA2AServer("logistics");

var app = builder.Build();
app.UseDemoDefaults();
app.MapA2AHttpJson("logistics", "/a2a/logistics");
app.MapGet("/cards/logistics", (HttpRequest request) => Card(request, "logistics", "Alternative per un ordine in ritardo."));
app.MapGet("/", () => "Logistica ADVANCE: /cards/logistics. Binding A2A HTTP+JSON.");
app.Run();

static AgentCard Card(HttpRequest request, string name, string description) => new()
{
    Name = name,
    Description = description,
    Version = "1.0",
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    SupportedInterfaces =
    [
        new()
        {
            Url = $"{request.Scheme}://{request.Host}/a2a/{name}",
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0"
        }
    ]
};

public partial class AdvanceA2AProgram;

using A2A;
using Basic.A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Shared.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices();
builder.Services.AddKeyedSingleton<AIAgent>("travel", (services, _) =>
    TravelTipsAgent.Create(services.GetRequiredService<IChatClient>()));
builder.AddA2AServer("travel");
var app = builder.Build();
app.UseDemoDefaults();
app.UseRemoteExecutionTelemetry("/a2a/travel");
app.MapA2AHttpJson("travel", "/a2a/travel");
app.MapGet("/cards/travel", (HttpRequest request) => new AgentCard
{
    Name = "travel",
    Description = "Due consigli di viaggio basati sul meteo.",
    Version = "1.0",
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    SupportedInterfaces =
    [
        new()
        {
            Url = $"{request.Scheme}://{request.Host}/a2a/travel",
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0"
        }
    ]
});
app.MapGet("/", () => "A2A BASIC: /cards/travel. Un agente e il meteo in memoria, senza Commerce.");
app.Run();

public partial class BasicA2AProgram;

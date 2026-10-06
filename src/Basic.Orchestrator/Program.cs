using Basic.Orchestrator;
using Basic.Skill;
using Shared.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("mcp", http => http.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddHttpClient("specialists", http =>
{
    http.BaseAddress = new Uri("http://specialists");
    http.Timeout = TimeSpan.FromMinutes(2);
});
var app = builder.Build();
app.UseDemoDefaults();
app.UseDemoUi("basic");
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapPost("/api/run", async (BasicRequest request, IChatClient model,
    IHttpClientFactory clients, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 2000 ||
        request.Technology is not ("mcp" or "a2a" or "skill"))
    {
        app.Logger.LogWarning("Richiesta BASIC non valida.");
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Inserisci una richiesta da 1 a 2000 caratteri e scegli mcp, a2a oppure skill."]
        });
    }
    var text = request.Technology switch
    {
        "mcp" => await McpDemo.RunAsync(model, clients.CreateClient("mcp"), request.Prompt, ct),
        "a2a" => await A2ADemo.RunAsync(model, clients.CreateClient("specialists"), request.Prompt, ct),
        _ => await SkillDemo.RunAsync(model, request.Prompt, ct)
    };
    return Results.Text(text);
}).WithSummary("BASIC: invia una richiesta a una singola tecnologia.")
    .Produces<string>(200, "text/plain").ProducesValidationProblem().ProducesProblem(502).ProducesProblem(503);
app.Run();

/// <summary>Richiesta indipendente per una tecnologia BASIC: mcp, a2a oppure skill.</summary>
public sealed record BasicRequest(string Prompt, string Technology);
public partial class BasicProgram;

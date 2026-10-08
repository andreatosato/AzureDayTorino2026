using Advance.Commerce;
using Shared.ServiceDefaults;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("specialists", http =>
{
    http.BaseAddress = new Uri("http://specialists");
    http.Timeout = TimeSpan.FromMinutes(2);
});
var app = builder.Build();
app.UseDemoDefaults();
app.UseDemoUi("advance");
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapPost("/api/run", async (AdvanceRequest request, IChatClient model,
    IHttpClientFactory clients, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 2000)
    {
        app.Logger.LogWarning("Richiesta ADVANCE non valida.");
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["prompt"] = ["Inserisci una richiesta da 1 a 2000 caratteri."]
        });
    }
    return Results.Text(await DemoTelemetry.TraceAsync("advance.run", () =>
        CommerceDemo.RunAsync(model, clients.CreateClient("specialists"), request.Prompt, ct)));
}).WithSummary("Advance.Web: Commerce con Skill, delega A2A e tool MCP.")
    .Produces<string>(200, "text/plain").ProducesValidationProblem().ProducesProblem(502).ProducesProblem(503);
app.Run();

/// <summary>Richiesta indipendente sullo scenario Commerce, senza operazioni di scrittura.</summary>
public sealed record AdvanceRequest(string Prompt);
public partial class AdvanceProgram;

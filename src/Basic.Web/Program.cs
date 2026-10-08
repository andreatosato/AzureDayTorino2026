using Basic.Orchestrator;
using Basic.Skill;
using Basic.Web;
using Shared.ServiceDefaults;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("mcp", http => http.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddTransient<RemoteExecutionHandler>();
builder.Services.AddHttpClient("specialists", http =>
{
    http.BaseAddress = new Uri("http://specialists");
    http.Timeout = TimeSpan.FromMinutes(2);
}).AddHttpMessageHandler<RemoteExecutionHandler>();
var app = builder.Build();
app.UseDemoDefaults();
app.UseDemoUi("basic");
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapPost("/api/run", async (BasicRequest request, IChatClient model,
    IHttpClientFactory clients, HttpContext context, CancellationToken ct) =>
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
    using var timings = new ExecutionTimings(request.Technology);
    try
    {
        RemoteAgentDetails? remoteAgent = null;
        string text;
        if (request.Technology == "a2a")
        {
            var a2a = await A2ADemo.RunWithDetailsAsync(model, clients.CreateClient("specialists"), request.Prompt, ct);
            text = a2a.Text;
            remoteAgent = a2a.Agent;
        }
        else
            text = request.Technology == "mcp"
                ? await McpDemo.RunAsync(model, clients.CreateClient("mcp"), request.Prompt, ct)
                : await SkillDemo.RunAsync(model, request.Prompt, ct);
        var execution = timings.Complete(text);
        var result = new BasicRunResponse(text, execution.TraceId, execution.DurationMs, execution.Steps,
            execution.InputTokens, execution.OutputTokens, remoteAgent);
        var wantsTimings = context.Request.GetTypedHeaders().Accept?.Any(
            accept => accept.MediaType.Value == "application/json" && accept.Quality is not 0) == true;
        return wantsTimings ? Results.Ok(result) : Results.Text(text);
    }
    catch (Exception exception)
    {
        timings.Fail(exception);
        throw;
    }
}).WithSummary("Basic.Web: invia una richiesta a MCP, A2A oppure Skill.")
    .WithDescription("Con Accept: application/json restituisce risposta, Trace ID, tempi e token Web, dati dello specialista A2A e totali Web + remoto. Altrimenti restituisce testo.")
    .Produces<string>(200, "text/plain").Produces<BasicRunResponse>(200, "application/json")
    .ProducesValidationProblem().ProducesProblem(502).ProducesProblem(503);
app.Run();

/// <summary>Richiesta indipendente per una tecnologia BASIC: mcp, a2a oppure skill.</summary>
public sealed record BasicRequest(string Prompt, string Technology);
public partial class BasicProgram;

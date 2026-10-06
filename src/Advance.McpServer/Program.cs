using Advance.McpServer;
using Shared.Hosting;
using Advance.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices(withModel: false);
builder.Services.AddHttpClient<CommerceTools>(http => http.BaseAddress = new Uri("http://commerce"));
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OrderTools>();

var app = builder.Build();
app.UseDemoDefaults();
app.MapMcp("/mcp");
app.MapGet("/", () => "MCP ADVANCE: /mcp (get_order, get_delivery_options). Nessun LLM.");
app.Run();

public partial class AdvanceMcpProgram;

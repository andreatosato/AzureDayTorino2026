using Basic.McpServer;
using Shared.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices(withModel: false);
builder.Services.AddMcpServer().WithHttpTransport().WithTools<WeatherTools>();
var app = builder.Build();
app.UseDemoDefaults();
app.MapMcp("/mcp");
app.MapGet("/", () => "MCP BASIC: /mcp, un solo tool get_weather e un dato in memoria. Nessun modello.");
app.Run();

public partial class BasicMcpProgram;

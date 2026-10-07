using Shared.ServiceDefaults;
using Advance.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoServices(withModel: false);
var app = builder.Build();
app.UseDemoDefaults();

app.MapGet("/", () => "Mock ADVANCE: GET /orders/ORD-1042 e GET /delivery-options?orderId=ORD-1042");
app.MapGet("/orders/{id}", (string id) =>
    Scenario.FindOrder(id) is { } order
        ? Results.Ok(order)
        : Results.Problem(statusCode: 404, title: "Ordine non trovato", detail: id));
app.MapGet("/delivery-options", (string orderId) =>
    Scenario.FindOrder(orderId) is not null
        ? Results.Ok(Scenario.Options(orderId))
        : Results.Problem(statusCode: 404, title: "Ordine non trovato", detail: orderId));

app.Run();

public partial class CommerceApiProgram;

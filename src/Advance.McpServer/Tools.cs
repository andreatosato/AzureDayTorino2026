using System.ComponentModel;
using Advance.Data;
using ModelContextProtocol.Server;

namespace Advance.McpServer;

[McpServerToolType]
public sealed class OrderTools(CommerceTools commerce)
{
    [McpServerTool(Name = "get_order"), Description("Leggi ordine, tracking e consegna prevista.")]
    public Task<Order> GetOrder(string orderId, CancellationToken cancellationToken) =>
        commerce.GetOrderAsync(orderId, cancellationToken);

    [McpServerTool(Name = "get_delivery_options"), Description("Leggi alternative, costi, orari e vincoli dell'ordine.")]
    public Task<DeliveryOption[]> GetDeliveryOptions(string orderId, CancellationToken cancellationToken) =>
        commerce.GetDeliveryOptionsAsync(orderId, cancellationToken);
}

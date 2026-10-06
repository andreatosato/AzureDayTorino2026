using System.ComponentModel;
using System.Net.Http.Json;

namespace Advance.Data;

public sealed class CommerceTools(HttpClient http)
{
    [Description("Leggi ordine, stato del tracking e consegna prevista per un ID ordine.")]
    public async Task<Order> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<Order>($"orders/{Uri.EscapeDataString(orderId)}", cancellationToken)
            ?? throw new InvalidOperationException("La API non ha restituito l'ordine.");
    }

    [Description("Leggi alternative di consegna con costi, orari e vincoli per un ordine.")]
    public async Task<DeliveryOption[]> GetDeliveryOptionsAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<DeliveryOption[]>(
            $"delivery-options?orderId={Uri.EscapeDataString(orderId)}", cancellationToken)
            ?? throw new InvalidOperationException("La API non ha restituito le alternative.");
    }
}

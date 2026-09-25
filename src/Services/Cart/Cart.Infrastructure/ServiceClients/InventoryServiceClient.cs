using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Cart.Application.Common;

namespace Ecommerce.Cart.Infrastructure.ServiceClients;

public class InventoryServiceClient : IInventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record StockResponse(Guid VariantId, int QuantityOnHand, int QuantityReserved, int QuantityAvailable);

    public async Task<int> GetAvailableQuantityAsync(Guid variantId, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/stock/{variantId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Todavía no existe registro de stock para esta variante (el evento de Catálogo
            // puede no haber llegado aún, o la variante es muy nueva): tratamos como "sin stock"
            // en vez de reventar, así el cliente ve un mensaje claro de "sin stock disponible"
            // en vez de un error 500 genérico.
            return 0;
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<StockResponse>(cancellationToken: ct);
        return body?.QuantityAvailable ?? 0;
    }
}

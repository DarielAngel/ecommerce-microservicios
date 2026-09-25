using System.Net;
using System.Net.Http.Json;
using Ecommerce.Cart.Application.Common;

namespace Ecommerce.Cart.Infrastructure.ServiceClients;

public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;

    public CatalogServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record CatalogVariantResponse(
        Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal Price, bool IsActive);

    public async Task<VariantInfo?> GetVariantAsync(Guid variantId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"api/products/variants/{variantId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CatalogVariantResponse>(cancellationToken: ct);

        return body is null
            ? null
            : new VariantInfo(body.VariantId, body.ProductId, body.ProductName, body.Sku, body.Price, body.IsActive);
    }
}

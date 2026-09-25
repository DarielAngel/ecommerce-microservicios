using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.Infrastructure.ServiceClients;

public class CartServiceClient : ICartServiceClient
{
    private readonly HttpClient _httpClient;

    public CartServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record CartItemResponse(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity);
    private record CartResponse(Guid UserId, List<CartItemResponse> Items);

    private HttpRequestMessage WithAuth(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<IReadOnlyList<CartItemInfo>> GetCartItemsAsync(string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(WithAuth(HttpMethod.Get, "api/cart", accessToken), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CartResponse>(cancellationToken: ct);

        return body?.Items
            .Select(i => new CartItemInfo(i.VariantId, i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity))
            .ToList() ?? new List<CartItemInfo>();
    }

    public async Task RemoveItemAsync(Guid variantId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Delete, $"api/cart/items/{variantId}", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }
}

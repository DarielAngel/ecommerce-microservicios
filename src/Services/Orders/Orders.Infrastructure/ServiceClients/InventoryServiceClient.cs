using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.Infrastructure.ServiceClients;

public class InventoryServiceClient : IInventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record ReservationLineRequest(Guid VariantId, int Quantity);
    private record ReserveStockRequest(Guid OrderId, List<ReservationLineRequest> Items);

    private HttpRequestMessage WithAuth(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<bool> ReserveStockAsync(
        Guid orderId, IReadOnlyList<ReservationLineInput> items, string accessToken, CancellationToken ct)
    {
        var body = new ReserveStockRequest(orderId, items.Select(i => new ReservationLineRequest(i.VariantId, i.Quantity)).ToList());

        var request = WithAuth(HttpMethod.Post, "api/stock/reservations", accessToken);
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false; // stock insuficiente: no es una excepción, es un resultado válido a manejar
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task ConfirmReservationAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"api/stock/reservations/{orderId}/confirm", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseReservationAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"api/stock/reservations/{orderId}/release", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }
}

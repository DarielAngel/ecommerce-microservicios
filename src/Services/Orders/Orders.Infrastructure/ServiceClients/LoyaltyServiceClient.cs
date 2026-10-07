using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.Infrastructure.ServiceClients;

/// <summary>
/// Habla con Lealtad por su ruta interna (/internal/loyalty/redemptions), que el Gateway no publica.
/// Reenvía el token del cliente: cada canje de puntos queda ligado a ese usuario.
/// </summary>
public class LoyaltyServiceClient : ILoyaltyServiceClient
{
    private readonly HttpClient _httpClient;

    public LoyaltyServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    private record ReserveRequest(Guid OrderId, decimal Amount);
    private record ReserveResponse(Guid OrderId, int Points, decimal DiscountAmount, string Status);
    private record ErrorResponse(string? Message);

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<LoyaltyReservation> ReserveAsync(Guid orderId, decimal amount, string accessToken, CancellationToken ct)
    {
        var request = WithAuth(HttpMethod.Post, "internal/loyalty/redemptions", accessToken);
        request.Content = JsonContent.Create(new ReserveRequest(orderId, amount));

        var response = await _httpClient.SendAsync(request, ct);

        // Puntos insuficientes o compra muy chica: no es un error del sistema, es una respuesta para el
        // cliente. Pasamos el mensaje de Lealtad tal cual.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
            throw new ConflictAppException(error?.Message ?? "No se pueden usar puntos en esta compra.");
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReserveResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Lealtad respondió sin cuerpo al reservar los puntos.");

        return new LoyaltyReservation(body.Points, body.DiscountAmount);
    }

    public async Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"internal/loyalty/redemptions/{orderId}/confirm", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"internal/loyalty/redemptions/{orderId}/release", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }
}

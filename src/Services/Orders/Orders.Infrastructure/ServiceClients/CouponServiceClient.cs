using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.Infrastructure.ServiceClients;

/// <summary>
/// Habla con Promociones por su ruta interna (/internal/redemptions), que el Gateway no publica.
/// Reenvía el token del cliente: cada uso del cupón queda ligado a ese usuario.
/// </summary>
public class CouponServiceClient : ICouponServiceClient
{
    private readonly HttpClient _httpClient;

    public CouponServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    private record ReserveRequest(Guid OrderId, string Code, decimal Subtotal);
    private record ReserveResponse(Guid OrderId, string Code, decimal Subtotal, decimal DiscountAmount, string Status);
    private record ErrorResponse(string? Message);

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<CouponReservation> ReserveAsync(
        Guid orderId, string code, decimal subtotal, string accessToken, CancellationToken ct)
    {
        var request = WithAuth(HttpMethod.Post, "internal/redemptions", accessToken);
        request.Content = JsonContent.Create(new ReserveRequest(orderId, code, subtotal));

        var response = await _httpClient.SendAsync(request, ct);

        // Cupón inexistente, vencido, agotado, bajo la compra mínima...: no es un error del sistema,
        // es una respuesta para el cliente. Pasamos el mensaje de Promociones tal cual.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
            throw new ConflictAppException(error?.Message ?? "El cupón no se puede aplicar a esta compra.");
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReserveResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Promociones respondió sin cuerpo al reservar el cupón.");

        return new CouponReservation(body.Code, body.DiscountAmount);
    }

    public async Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"internal/redemptions/{orderId}/confirm", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"internal/redemptions/{orderId}/release", accessToken), ct);
        response.EnsureSuccessStatusCode();
    }
}

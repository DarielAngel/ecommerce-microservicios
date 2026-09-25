using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.Infrastructure.ServiceClients;

public class PaymentServiceClient : IPaymentServiceClient
{
    private readonly HttpClient _httpClient;

    public PaymentServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record CreatePaymentRequest(Guid OrderId, decimal Amount, string Currency);
    private record PaymentResponse(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Status, string? ApproveUrl);

    private HttpRequestMessage WithAuth(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<CreatePaymentResult> CreatePaymentAsync(
        Guid orderId, decimal amount, string currency, string accessToken, CancellationToken ct)
    {
        var request = WithAuth(HttpMethod.Post, "api/payments", accessToken);
        request.Content = JsonContent.Create(new CreatePaymentRequest(orderId, amount, currency));

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: ct);
        return new CreatePaymentResult(body?.Status ?? "Unknown", body?.ApproveUrl);
    }

    public async Task<CapturePaymentResult> CapturePaymentAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(
            WithAuth(HttpMethod.Post, $"api/payments/{orderId}/capture", accessToken), ct);

        if (!response.IsSuccessStatusCode)
        {
            // 409 (PayPal rechazó la captura) u otro error: lo tratamos como "no exitoso",
            // no como una excepción — el orquestador decide qué hacer (liberar stock, marcar fallida).
            return new CapturePaymentResult(false, response.StatusCode.ToString());
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: ct);
        return new CapturePaymentResult(body?.Status == "Captured", body?.Status ?? "Unknown");
    }
}

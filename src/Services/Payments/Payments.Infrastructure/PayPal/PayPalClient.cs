using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Payments.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Infrastructure.PayPal;

public class PayPalClient : IPayPalClient
{
    private readonly HttpClient _httpClient;
    private readonly PayPalAccessTokenProvider _tokenProvider;
    private readonly PayPalSettings _settings;
    private readonly ILogger<PayPalClient> _logger;

    public PayPalClient(
        HttpClient httpClient,
        PayPalAccessTokenProvider tokenProvider,
        IOptions<PayPalSettings> settings,
        ILogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    // ---- DTOs mínimos de la API de PayPal (solo los campos que realmente usamos) ----

    private record CreateOrderRequest(string intent, PurchaseUnit[] purchase_units, ApplicationContext application_context);
    private record PurchaseUnit(string reference_id, Amount amount);
    private record Amount(string currency_code, string value);
    private record ApplicationContext(string return_url, string cancel_url, string user_action);

    private record PayPalLink(string href, string rel, string method);
    private record CreateOrderResponse(string id, PayPalLink[] links);

    private record CaptureResponse(string id, string status, PurchaseUnitCapture[]? purchase_units);
    private record PurchaseUnitCapture(Payments payments);
    private record Payments(Capture[]? captures);
    private record Capture(string id, string status);

    private record RefundRequest(Amount amount, string? note_to_payer);
    private record RefundResponse(string id, string status);

    private record VerifyWebhookRequest(
        string transmission_id, string transmission_time, string cert_url, string auth_algo,
        string transmission_sig, string webhook_id, JsonElement webhook_event);
    private record VerifyWebhookResponse(string verification_status);

    // ---- Implementación ----

    public async Task<CreatePayPalOrderResult> CreateOrderAsync(
        decimal amount, string currency, Guid internalOrderId, CancellationToken ct)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        var body = new CreateOrderRequest(
            intent: "CAPTURE",
            purchase_units: new[]
            {
                new PurchaseUnit(
                    reference_id: internalOrderId.ToString(),
                    amount: new Amount(currency.ToUpperInvariant(), amount.ToString("F2", CultureInfo.InvariantCulture)))
            },
            application_context: new ApplicationContext(_settings.ReturnUrl, _settings.CancelUrl, "PAY_NOW"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Idempotency key: si esta misma llamada se reintenta (timeout de red, etc.), PayPal
        // devuelve la MISMA orden en vez de crear una duplicada.
        request.Headers.Add("PayPal-Request-Id", $"create-{internalOrderId}");
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal rechazó la creación de la orden ({Status}): {Body}", response.StatusCode, responseBody);
            throw new PayPalCommunicationException($"PayPal rechazó la creación de la orden: {responseBody}");
        }

        var parsed = JsonSerializer.Deserialize<CreateOrderResponse>(responseBody)
            ?? throw new PayPalCommunicationException("Respuesta de creación de orden de PayPal vacía o inválida.");

        var approveLink = parsed.links?.FirstOrDefault(l => l.rel == "approve")?.href
            ?? throw new PayPalCommunicationException("PayPal no devolvió un link de aprobación (approve) para la orden.");

        return new CreatePayPalOrderResult(parsed.id, approveLink);
    }

    public async Task<CapturePayPalOrderResult> CaptureOrderAsync(string payPalOrderId, CancellationToken ct)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{payPalOrderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("PayPal-Request-Id", $"capture-{payPalOrderId}");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal rechazó la captura ({Status}): {Body}", response.StatusCode, responseBody);
            return new CapturePayPalOrderResult(false, null, responseBody);
        }

        var parsed = JsonSerializer.Deserialize<CaptureResponse>(responseBody);
        var captureId = parsed?.purchase_units?.FirstOrDefault()?.payments.captures?.FirstOrDefault()?.id;

        if (parsed?.status != "COMPLETED" || captureId is null)
        {
            return new CapturePayPalOrderResult(false, null, $"Estado inesperado de PayPal: {parsed?.status}");
        }

        return new CapturePayPalOrderResult(true, captureId, null);
    }

    public async Task<RefundPayPalCaptureResult> RefundCaptureAsync(
        string captureId, decimal amount, string currency, string requestId, string? note, CancellationToken ct)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Misma idea que al crear y capturar: si se reintenta con la misma clave, PayPal devuelve el mismo
        // reembolso en vez de devolver el dinero dos veces.
        request.Headers.Add("PayPal-Request-Id", $"refund-{requestId}");
        var noteToPayer = string.IsNullOrWhiteSpace(note) ? null : note.Length > 255 ? note[..255] : note;
        request.Content = JsonContent.Create(new RefundRequest(
            new Amount(currency.ToUpperInvariant(), amount.ToString("F2", CultureInfo.InvariantCulture)), noteToPayer));

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal rechazó el reembolso de {CaptureId} ({Status}): {Body}", captureId, response.StatusCode, responseBody);
            return new RefundPayPalCaptureResult(false, null, null, $"PayPal rechazó el reembolso ({(int)response.StatusCode}).");
        }

        var parsed = JsonSerializer.Deserialize<RefundResponse>(responseBody);
        // PENDING también es un reembolso aceptado (PayPal lo termina después, por ejemplo con eCheck).
        if (parsed?.id is null || parsed.status is not ("COMPLETED" or "PENDING"))
        {
            return new RefundPayPalCaptureResult(false, null, parsed?.status, $"Estado inesperado del reembolso en PayPal: {parsed?.status}");
        }

        return new RefundPayPalCaptureResult(true, parsed.id, parsed.status, null);
    }

    public async Task<bool> VerifyWebhookSignatureAsync(IDictionary<string, string> headers, string rawBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookId))
        {
            // Sin WebhookId configurado no podemos verificar nada — más vale rechazar todo
            // que aceptar webhooks sin verificar (ver README para cómo obtenerlo).
            _logger.LogWarning("PayPal WebhookId no configurado: rechazando el webhook por seguridad.");
            return false;
        }

        if (!TryGetHeader(headers, "PAYPAL-TRANSMISSION-ID", out var transmissionId) ||
            !TryGetHeader(headers, "PAYPAL-TRANSMISSION-TIME", out var transmissionTime) ||
            !TryGetHeader(headers, "PAYPAL-CERT-URL", out var certUrl) ||
            !TryGetHeader(headers, "PAYPAL-AUTH-ALGO", out var authAlgo) ||
            !TryGetHeader(headers, "PAYPAL-TRANSMISSION-SIG", out var transmissionSig))
        {
            _logger.LogWarning("Webhook de PayPal sin los headers de firma esperados — rechazado.");
            return false;
        }

        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        using var webhookEventDocument = JsonDocument.Parse(rawBody);

        var verifyBody = new VerifyWebhookRequest(
            transmissionId, transmissionTime, certUrl, authAlgo, transmissionSig,
            _settings.WebhookId, webhookEventDocument.RootElement);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications/verify-webhook-signature");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(verifyBody);

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Fallo al verificar la firma del webhook contra PayPal ({Status}).", response.StatusCode);
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<VerifyWebhookResponse>(cancellationToken: ct);
        return result?.verification_status == "SUCCESS";
    }

    private static bool TryGetHeader(IDictionary<string, string> headers, string name, out string value)
    {
        // Los nombres de header pueden venir con distinta capitalización según el cliente HTTP.
        var match = headers.FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase));
        value = match.Value ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }
}

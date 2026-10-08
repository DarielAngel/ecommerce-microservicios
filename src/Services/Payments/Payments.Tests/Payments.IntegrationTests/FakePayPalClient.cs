using System.Collections.Concurrent;
using Ecommerce.Payments.Application.Common;

namespace Ecommerce.Payments.IntegrationTests;

/// <summary>
/// Simula PayPal en memoria: genera Ids falsos consistentes y permite forzar un fallo
/// de captura desde el test (FailCaptureFor). No hay red real de por medio.
/// </summary>
public class FakePayPalClient : IPayPalClient
{
    public ConcurrentDictionary<string, bool> CapturedOrders { get; } = new();
    public HashSet<string> OrdersThatShouldFailCapture { get; } = new();
    public bool AlwaysValidWebhookSignature { get; set; } = true;

    public Task<CreatePayPalOrderResult> CreateOrderAsync(decimal amount, string currency, Guid internalOrderId, CancellationToken ct)
    {
        var paypalOrderId = $"FAKE-PP-{internalOrderId:N}";
        return Task.FromResult(new CreatePayPalOrderResult(paypalOrderId, $"https://fake-paypal.test/approve/{paypalOrderId}"));
    }

    public Task<CapturePayPalOrderResult> CaptureOrderAsync(string payPalOrderId, CancellationToken ct)
    {
        if (OrdersThatShouldFailCapture.Contains(payPalOrderId))
        {
            return Task.FromResult(new CapturePayPalOrderResult(false, null, "Fallo simulado para pruebas"));
        }

        var captureId = $"FAKE-CAPTURE-{payPalOrderId}";
        CapturedOrders[payPalOrderId] = true;
        return Task.FromResult(new CapturePayPalOrderResult(true, captureId, null));
    }

    public ConcurrentDictionary<string, decimal> Refunds { get; } = new();
    public bool FailRefunds { get; set; }

    public Task<RefundPayPalCaptureResult> RefundCaptureAsync(
        string captureId, decimal amount, string currency, string requestId, string? note, CancellationToken ct)
    {
        if (FailRefunds)
            return Task.FromResult(new RefundPayPalCaptureResult(false, null, null, "Reembolso rechazado (simulado)."));
        Refunds.AddOrUpdate(requestId, amount, (_, previous) => previous); // misma clave = mismo reembolso
        return Task.FromResult(new RefundPayPalCaptureResult(true, $"FAKE-REFUND-{requestId}", "COMPLETED", null));
    }

    public Task<bool> VerifyWebhookSignatureAsync(IDictionary<string, string> headers, string rawBody, CancellationToken ct) =>
        Task.FromResult(AlwaysValidWebhookSignature);
}

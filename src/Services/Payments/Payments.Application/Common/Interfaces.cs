using Ecommerce.Payments.Domain.Entities;

namespace Ecommerce.Payments.Application.Common;

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<Payment?> GetByPayPalOrderIdAsync(string payPalOrderId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public record CreatePayPalOrderResult(string PayPalOrderId, string ApproveUrl);
public record CapturePayPalOrderResult(bool Success, string? CaptureId, string? FailureReason);

/// <summary>Abstrae la comunicación con la API real de PayPal (sandbox o producción).</summary>
public interface IPayPalClient
{
    Task<CreatePayPalOrderResult> CreateOrderAsync(decimal amount, string currency, Guid internalOrderId, CancellationToken ct);
    Task<CapturePayPalOrderResult> CaptureOrderAsync(string payPalOrderId, CancellationToken ct);

    /// <summary>Verifica que un webhook entrante realmente venga de PayPal (evita webhooks falsificados).</summary>
    Task<bool> VerifyWebhookSignatureAsync(IDictionary<string, string> headers, string rawBody, CancellationToken ct);
}

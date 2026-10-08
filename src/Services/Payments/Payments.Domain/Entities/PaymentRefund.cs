namespace Ecommerce.Payments.Domain.Entities;

/// <summary>
/// Un reembolso (total o parcial) de un pago capturado. Su Id lo elige quien lo pide (Órdenes usa el id de
/// la devolución): así, pedir dos veces el mismo reembolso devuelve el que ya existe en vez de pagar dos veces.
/// </summary>
public class PaymentRefund
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string PayPalRefundId { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentRefund() { }

    internal PaymentRefund(Guid id, decimal amount, string payPalRefundId, string? reason, DateTime createdAtUtc)
    {
        Id = id;
        Amount = amount;
        PayPalRefundId = payPalRefundId;
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }
}

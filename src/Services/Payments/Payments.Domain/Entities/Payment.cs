using Ecommerce.Payments.Domain.Enums;
using Ecommerce.Payments.Domain.Exceptions;

namespace Ecommerce.Payments.Domain.Entities;

public class Payment
{
    private readonly List<PaymentRefund> _refunds = new();

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string PayPalOrderId { get; private set; } = null!;
    public string? PayPalCaptureId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? CapturedAtUtc { get; private set; }

    /// <summary>Reembolsos hechos sobre este pago (Fase 7). Puede haber varios parciales.</summary>
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public decimal RefundedAmount => _refunds.Sum(r => r.Amount);

    /// <summary>Lo que todavía se puede devolver.</summary>
    public decimal RefundableAmount => Status is PaymentStatus.Captured or PaymentStatus.Refunded
        ? Amount - RefundedAmount
        : 0m;

    private Payment() { }

    private Payment(Guid orderId, decimal amount, string currency, string payPalOrderId)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        PayPalOrderId = payPalOrderId;
        Status = PaymentStatus.PendingApproval;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static Payment Create(Guid orderId, decimal amount, string currency, string payPalOrderId)
    {
        if (amount <= 0)
        {
            throw new DomainException("El monto a pagar debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new DomainException("La moneda debe ser un código ISO de 3 letras (ej. USD).");
        }

        if (string.IsNullOrWhiteSpace(payPalOrderId))
        {
            throw new DomainException("Falta el Id de la orden de PayPal.");
        }

        return new Payment(orderId, amount, currency.ToUpperInvariant(), payPalOrderId);
    }

    /// <summary>
    /// Idempotente a propósito: PayPal puede reintentar/reenviar la confirmación (webhook
    /// duplicado, o el caller vuelve a llamar a /capture). Si ya está capturado con el MISMO
    /// captureId, no hace nada. Si el capture ya existe con un id DISTINTO, algo está mal y
    /// preferimos fallar ruidosamente en vez de silenciarlo.
    /// </summary>
    public void MarkCaptured(string captureId)
    {
        if (Status == PaymentStatus.Captured)
        {
            if (PayPalCaptureId != captureId)
            {
                throw new DomainException(
                    "Este pago ya fue capturado con un captureId distinto. Requiere revisión manual.");
            }

            return; // ya estaba capturado con el mismo id: no-op idempotente
        }

        if (Status is PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Refunded)
        {
            throw new DomainException($"No se puede capturar un pago en estado '{Status}'.");
        }

        Status = PaymentStatus.Captured;
        PayPalCaptureId = captureId;
        CapturedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
        {
            throw new DomainException("No se puede marcar como fallido un pago que ya fue capturado.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        if (Status == PaymentStatus.Captured)
        {
            throw new DomainException("No se puede cancelar un pago que ya fue capturado.");
        }

        Status = PaymentStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public PaymentRefund? FindRefund(Guid refundId) => _refunds.FirstOrDefault(r => r.Id == refundId);

    /// <summary>
    /// Verifica ANTES de llamar a PayPal que el reembolso es posible (pago capturado, monto válido y que no
    /// supere lo que queda por devolver). Así no se le pide a PayPal algo que después no se puede registrar.
    /// </summary>
    public void EnsureCanRefund(decimal amount)
    {
        if (Status is not (PaymentStatus.Captured or PaymentStatus.Refunded))
            throw new DomainException("Solo se puede reembolsar un pago que ya fue capturado.");
        if (amount <= 0)
            throw new DomainException("El monto a reembolsar debe ser mayor a cero.");
        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("El monto a reembolsar admite como máximo 2 decimales.");
        if (amount > RefundableAmount)
            throw new DomainException(
                $"No se puede reembolsar {amount:F2} {Currency}: de este pago quedan {RefundableAmount:F2} {Currency} por devolver.");
    }

    /// <summary>Registra un reembolso que PayPal ya aceptó. Al devolver todo, el pago queda en Refunded.</summary>
    public PaymentRefund AddRefund(Guid refundId, decimal amount, string payPalRefundId, string? reason, DateTime nowUtc)
    {
        if (refundId == Guid.Empty) throw new DomainException("El reembolso necesita un identificador.");
        if (FindRefund(refundId) is not null) throw new DomainException("Ese reembolso ya estaba registrado.");
        if (string.IsNullOrWhiteSpace(payPalRefundId)) throw new DomainException("Falta el Id del reembolso de PayPal.");
        EnsureCanRefund(amount);

        var refund = new PaymentRefund(refundId, amount, payPalRefundId, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), nowUtc);
        _refunds.Add(refund);
        if (RefundedAmount == Amount) Status = PaymentStatus.Refunded;
        UpdatedAtUtc = nowUtc;
        return refund;
    }
}

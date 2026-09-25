namespace Ecommerce.Payments.Domain.Enums;

public enum PaymentStatus
{
    /// <summary>Orden creada en PayPal, esperando que el comprador la apruebe.</summary>
    PendingApproval = 0,

    /// <summary>Capturado con éxito: el dinero ya se movió.</summary>
    Captured = 1,

    /// <summary>Falló la captura, o PayPal reportó un fallo vía webhook.</summary>
    Failed = 2,

    /// <summary>El comprador canceló antes de aprobar.</summary>
    Cancelled = 3,

    /// <summary>Reembolsado después de haber sido capturado.</summary>
    Refunded = 4
}

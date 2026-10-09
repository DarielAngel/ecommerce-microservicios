using Ecommerce.Promotions.Domain.Exceptions;

namespace Ecommerce.Promotions.Domain.Entities;

public enum RedemptionStatus
{
    /// <summary>Apartado durante el checkout: cuenta para el límite de usos mientras se paga.</summary>
    Reserved = 0,

    /// <summary>El pago se capturó: el uso es definitivo.</summary>
    Confirmed = 1,

    /// <summary>El pago falló o se canceló: el uso vuelve a estar disponible.</summary>
    Released = 2,

    /// <summary>
    /// La compra se pagó pero después se reembolsó entera (se canceló antes del envío o se devolvió todo — Fase 7):
    /// el uso deja de contar, como si no se hubiera comprado. El registro queda para el historial.
    /// </summary>
    Restored = 3
}

/// <summary>
/// Un uso de un cupón ligado a UNA orden. Sigue el mismo ciclo que la reserva de stock en Inventario
/// (reservar → confirmar o liberar), así un cupón de usos limitados nunca se gasta en una compra que
/// no se completó. La clave es el OrderId: reintentar con la misma orden nunca cuenta dos usos.
/// </summary>
public class CouponRedemption
{
    /// <summary>
    /// Una reserva que nunca se confirmó (el cliente abandonó el pago en PayPal) deja de contar para el
    /// límite pasado este tiempo, para que no bloquee el cupón para siempre.
    /// </summary>
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromHours(2);

    public Guid OrderId { get; private set; }
    public Guid CouponId { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = null!;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public RedemptionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private CouponRedemption() { }

    public static CouponRedemption Reserve(Guid orderId, Coupon coupon, Guid userId, decimal subtotal, decimal discountAmount)
    {
        if (orderId == Guid.Empty) throw new DomainException("La orden es obligatoria.");
        if (userId == Guid.Empty) throw new DomainException("El usuario es obligatorio.");

        var now = DateTime.UtcNow;
        return new CouponRedemption
        {
            OrderId = orderId,
            CouponId = coupon.Id,
            UserId = userId,
            Code = coupon.Code,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            Status = RedemptionStatus.Reserved,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>Idempotente: confirmar dos veces no hace nada la segunda.</summary>
    public void Confirm()
    {
        // Restored: la compra ya se pagó y se reembolsó; una confirmación que llega tarde no la vuelve a contar.
        if (Status is RedemptionStatus.Confirmed or RedemptionStatus.Restored) return;
        if (Status == RedemptionStatus.Released)
            throw new DomainException("No se puede confirmar el uso de un cupón que ya se liberó.");

        Status = RedemptionStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Idempotente: liberar dos veces no hace nada la segunda.</summary>
    public void Release()
    {
        if (Status is RedemptionStatus.Released or RedemptionStatus.Restored) return;
        if (Status == RedemptionStatus.Confirmed)
            throw new DomainException("No se puede liberar el uso de un cupón de una compra ya pagada.");

        Status = RedemptionStatus.Released;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// La compra se reembolsó completa: el uso del cupón vuelve a estar disponible (para el límite de usos y para
    /// "un uso por cliente"). Idempotente. Una reserva que nunca se confirmó (la confirmación es "mejor esfuerzo")
    /// también se puede devolver; una liberada ya no cuenta, así que no cambia.
    /// </summary>
    /// <returns>true si cambió (antes contaba como usado).</returns>
    public bool Restore()
    {
        if (Status is RedemptionStatus.Restored or RedemptionStatus.Released) return false;

        Status = RedemptionStatus.Restored;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}

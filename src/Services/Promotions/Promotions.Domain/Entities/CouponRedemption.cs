using Ecommerce.Promotions.Domain.Exceptions;

namespace Ecommerce.Promotions.Domain.Entities;

public enum RedemptionStatus
{
    /// <summary>Apartado durante el checkout: cuenta para el límite de usos mientras se paga.</summary>
    Reserved = 0,

    /// <summary>El pago se capturó: el uso es definitivo.</summary>
    Confirmed = 1,

    /// <summary>El pago falló o se canceló: el uso vuelve a estar disponible.</summary>
    Released = 2
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
        if (Status == RedemptionStatus.Confirmed) return;
        if (Status == RedemptionStatus.Released)
            throw new DomainException("No se puede confirmar el uso de un cupón que ya se liberó.");

        Status = RedemptionStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Idempotente: liberar dos veces no hace nada la segunda.</summary>
    public void Release()
    {
        if (Status == RedemptionStatus.Released) return;
        if (Status == RedemptionStatus.Confirmed)
            throw new DomainException("No se puede liberar el uso de un cupón de una compra ya pagada.");

        Status = RedemptionStatus.Released;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

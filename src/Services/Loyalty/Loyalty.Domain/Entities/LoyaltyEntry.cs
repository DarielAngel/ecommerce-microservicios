using Ecommerce.Loyalty.Domain.Exceptions;

namespace Ecommerce.Loyalty.Domain.Entities;

public enum LoyaltyEntryKind
{
    /// <summary>Puntos ganados por una compra pagada.</summary>
    Earned = 0,

    /// <summary>Puntos usados como descuento en una compra.</summary>
    Redeemed = 1,

    /// <summary>Se descuentan puntos ganados porque esa parte de la compra se devolvió (Fase 7).</summary>
    Reversed = 2,

    /// <summary>Vuelven puntos que se habían usado en una compra que se devolvió (Fase 7).</summary>
    Restored = 3
}

public enum LoyaltyEntryStatus
{
    /// <summary>Canje apartado durante el checkout (todavía se puede deshacer).</summary>
    Reserved = 0,

    /// <summary>Definitivo: puntos ganados, o canje de una compra que se pagó.</summary>
    Confirmed = 1,

    /// <summary>Canje deshecho: el pago falló o se canceló, los puntos vuelven al saldo.</summary>
    Released = 2
}

/// <summary>
/// Un movimiento de puntos ligado a UNA orden. El saldo nunca se guarda: se calcula sumando los
/// movimientos (ver <see cref="PointsLedger"/>), así no hay forma de que quede desincronizado.
/// Una orden tiene como mucho un movimiento de cada tipo por referencia (lo garantiza un índice único): reintentar
/// la misma orden (o la misma devolución) nunca suma ni descuenta dos veces.
/// </summary>
public class LoyaltyEntry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrderId { get; private set; }
    public LoyaltyEntryKind Kind { get; private set; }

    /// <summary>
    /// Para los movimientos de una devolución (Reversed / Restored): el id de la devolución. Vacío en los demás.
    /// Una orden puede tener varias devoluciones parciales, cada una con sus movimientos.
    /// </summary>
    public Guid ReferenceId { get; private set; }
    public int Points { get; private set; }

    /// <summary>Solo en canjes: cuánto se descontó de la compra.</summary>
    public decimal DiscountAmount { get; private set; }

    public LoyaltyEntryStatus Status { get; private set; }

    /// <summary>
    /// Solo en canjes apartados: hasta cuándo descuentan del saldo si no se pagan. Antes de capturar el
    /// pago, Órdenes vuelve a apartar (renueva); si para entonces los puntos ya no alcanzan, la compra
    /// no se cobra (ver ReservePointsCommandHandler).
    /// </summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private LoyaltyEntry() { }

    private static void Require(Guid userId, Guid orderId)
    {
        if (userId == Guid.Empty) throw new DomainException("El cliente es obligatorio.");
        if (orderId == Guid.Empty) throw new DomainException("La orden es obligatoria.");
    }

    /// <summary>Puntos por una compra pagada. Devuelve null si el monto no alcanza para 1 punto.</summary>
    public static LoyaltyEntry? Earn(Guid userId, Guid orderId, decimal amountPaid, DateTime nowUtc)
    {
        Require(userId, orderId);
        var points = LoyaltyRules.PointsEarnedFor(amountPaid);
        if (points == 0) return null;

        return new LoyaltyEntry
        {
            Id = Guid.NewGuid(), UserId = userId, OrderId = orderId, Kind = LoyaltyEntryKind.Earned,
            Points = points, Status = LoyaltyEntryStatus.Confirmed, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
        };
    }

    public static LoyaltyEntry Reserve(Guid userId, Guid orderId, RedemptionQuote quote, DateTime nowUtc)
    {
        Require(userId, orderId);
        if (quote.Points < LoyaltyRules.MinRedeemPoints)
            throw new DomainException($"Para usar puntos necesitas al menos {LoyaltyRules.MinRedeemPoints}.");

        return new LoyaltyEntry
        {
            Id = Guid.NewGuid(), UserId = userId, OrderId = orderId, Kind = LoyaltyEntryKind.Redeemed,
            Points = quote.Points, DiscountAmount = quote.Discount, Status = LoyaltyEntryStatus.Reserved,
            ExpiresAtUtc = nowUtc + LoyaltyRules.ReservationTtl, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>Movimiento por una devolución reembolsada (Fase 7): descontar ganados o devolver usados.</summary>
    public static LoyaltyEntry ForRefund(Guid userId, Guid orderId, Guid returnId, LoyaltyEntryKind kind, int points, DateTime nowUtc)
    {
        Require(userId, orderId);
        if (returnId == Guid.Empty) throw new DomainException("La devolución es obligatoria.");
        if (kind is not (LoyaltyEntryKind.Reversed or LoyaltyEntryKind.Restored))
            throw new DomainException("Una devolución solo descuenta puntos ganados o devuelve puntos usados.");
        if (points <= 0) throw new DomainException("Los puntos deben ser mayores que 0.");

        return new LoyaltyEntry
        {
            Id = Guid.NewGuid(), UserId = userId, OrderId = orderId, ReferenceId = returnId, Kind = kind,
            Points = points, Status = LoyaltyEntryStatus.Confirmed, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>¿Suma al saldo (ganados, devueltos) o resta (usados, descontados)?</summary>
    public bool Adds => Kind is LoyaltyEntryKind.Earned or LoyaltyEntryKind.Restored;

    /// <summary>¿Es un canje apartado cuyo plazo ya venció (dejó de descontar del saldo)?</summary>
    public bool IsExpiredAt(DateTime nowUtc) => Status == LoyaltyEntryStatus.Reserved && ExpiresAtUtc <= nowUtc;

    /// <summary>Vuelve a apartar por otro plazo completo (quien llama ya verificó que el saldo alcanza).</summary>
    public void Renew(DateTime nowUtc)
    {
        if (Status != LoyaltyEntryStatus.Reserved)
            throw new DomainException("Solo se puede renovar un canje de puntos apartado.");
        ExpiresAtUtc = nowUtc + LoyaltyRules.ReservationTtl;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Idempotente.</summary>
    public void Confirm(DateTime nowUtc)
    {
        if (Status == LoyaltyEntryStatus.Confirmed) return;
        if (Status == LoyaltyEntryStatus.Released)
            throw new DomainException("No se puede confirmar un canje de puntos que ya se deshizo.");
        Status = LoyaltyEntryStatus.Confirmed;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Idempotente.</summary>
    public void Release(DateTime nowUtc)
    {
        if (Status == LoyaltyEntryStatus.Released) return;
        if (Status == LoyaltyEntryStatus.Confirmed)
            throw new DomainException("No se puede deshacer el canje de puntos de una compra ya pagada.");
        Status = LoyaltyEntryStatus.Released;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>¿Este movimiento cuenta hoy para el saldo?</summary>
    public bool CountsAt(DateTime nowUtc) => Status switch
    {
        LoyaltyEntryStatus.Confirmed => true,
        LoyaltyEntryStatus.Reserved => ExpiresAtUtc > nowUtc,
        _ => false
    };
}

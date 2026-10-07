namespace Ecommerce.Loyalty.Domain.Entities;

/// <summary>
/// Reglas del programa de puntos (Fase 6, T6.1). Están en un solo lugar para que la tienda las muestre
/// tal cual (las expone GET /api/loyalty/me) y nunca haya dos versiones distintas.
/// </summary>
public static class LoyaltyRules
{
    /// <summary>Se gana 1 punto por cada dólar pagado (lo que se cobró, ya con descuentos).</summary>
    public const int PointsPerDollar = 1;

    /// <summary>Cada punto vale 1 centavo al canjearlo: 100 puntos = US$ 1.</summary>
    public const decimal PointValue = 0.01m;

    /// <summary>Hace falta juntar al menos esto para poder usar puntos.</summary>
    public const int MinRedeemPoints = 100;

    /// <summary>Con puntos se paga como máximo la mitad de la compra (lo que queda después del cupón).</summary>
    public const decimal MaxRedeemShare = 0.5m;

    /// <summary>Un canje apartado en un checkout que nunca se pagó deja de contar pasado este tiempo.</summary>
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromHours(2);

    public static int PointsEarnedFor(decimal amountPaid) =>
        amountPaid <= 0 ? 0 : (int)Math.Floor(amountPaid * PointsPerDollar);

    public static decimal ValueOf(int points) => decimal.Round(points * PointValue, 2);

    /// <summary>
    /// Cuántos puntos se pueden usar en una compra de <paramref name="amount"/> teniendo
    /// <paramref name="balance"/>: todos los que alcancen sin pasar el tope del 50 %. Si no llega al
    /// mínimo, 0 (no se ofrece usar puntos).
    /// </summary>
    public static RedemptionQuote Quote(int balance, decimal amount)
    {
        if (balance < MinRedeemPoints || amount <= 0) return RedemptionQuote.None;

        var maxByShare = (int)Math.Floor(amount * MaxRedeemShare / PointValue);
        var points = Math.Min(balance, maxByShare);
        return points < MinRedeemPoints ? RedemptionQuote.None : new RedemptionQuote(points, ValueOf(points));
    }
}

public record RedemptionQuote(int Points, decimal Discount)
{
    public static RedemptionQuote None { get; } = new(0, 0m);
}

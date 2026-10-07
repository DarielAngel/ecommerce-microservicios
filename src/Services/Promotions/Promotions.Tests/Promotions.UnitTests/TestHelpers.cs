using Ecommerce.Promotions.Domain.Entities;

namespace Ecommerce.Promotions.UnitTests;

/// <summary>Reloj fijo: "ahora" es lo que el test diga.</summary>
public class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public static class Coupons
{
    public static Coupon Percentage(decimal value = 10, decimal? max = null, decimal minimum = 0, int? limit = null,
        bool oncePerCustomer = false, DateTime? starts = null, DateTime? ends = null, string code = "VERANO10") =>
        Coupon.Create(code, "Descuento de verano", DiscountType.Percentage, value, max, minimum, starts, ends, limit, oncePerCustomer);

    public static Coupon Fixed(decimal value = 10, decimal minimum = 0, int? limit = null, string code = "MENOS10") =>
        Coupon.Create(code, "Diez dólares menos", DiscountType.FixedAmount, value, null, minimum, null, null, limit, false);
}

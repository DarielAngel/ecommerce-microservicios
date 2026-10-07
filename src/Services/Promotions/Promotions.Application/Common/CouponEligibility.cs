using Ecommerce.Promotions.Domain.Entities;
using Ecommerce.Promotions.Domain.Exceptions;

namespace Ecommerce.Promotions.Application.Common;

/// <summary>
/// Las reglas que dependen de la base de datos (límite de usos, un uso por cliente). Las comparten la
/// validación desde el checkout y el canje real, para que nunca den respuestas distintas.
/// </summary>
public static class CouponEligibility
{
    public static async Task<decimal> EnsureApplicableAsync(
        Coupon coupon, Guid userId, decimal subtotal, DateTime nowUtc, IRedemptionRepository redemptions, CancellationToken ct)
    {
        // Primero las reglas propias del cupón (vigencia, compra mínima...): no requieren la base.
        var discount = coupon.CalculateDiscount(subtotal, nowUtc);

        if (coupon.UsageLimit is not null)
        {
            var usage = await redemptions.GetUsageAsync(coupon.Id, nowUtc, ct);
            if (usage.Total >= coupon.UsageLimit)
                throw new CouponNotApplicableException("Este cupón ya alcanzó su límite de usos.");
        }

        if (coupon.OncePerCustomer && await redemptions.HasActiveForUserAsync(coupon.Id, userId, nowUtc, ct))
            throw new CouponNotApplicableException("Ya usaste este cupón: es de un solo uso por cliente.");

        return discount;
    }
}

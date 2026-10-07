using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Promotions.Application.Features;

/// <summary>Lo que el cliente ve al aplicar un cupón en el checkout, antes de pagar.</summary>
public record CouponQuoteResult(string Code, string Description, decimal Subtotal, decimal DiscountAmount, decimal Total);

/// <summary>
/// "¿Cuánto me descuenta este cupón?" — solo consulta, no aparta nada. El canje real ocurre en el
/// checkout (ReserveRedemption), que vuelve a verificar todo por si algo cambió entre medio.
/// </summary>
public record ValidateCouponQuery(string Code, Guid UserId, decimal Subtotal) : IRequest<CouponQuoteResult>;

public class ValidateCouponQueryValidator : AbstractValidator<ValidateCouponQuery>
{
    public ValidateCouponQueryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Escribe un código de cupón.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.Subtotal).GreaterThan(0).WithMessage("Agrega productos antes de aplicar un cupón.");
    }
}

public class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, CouponQuoteResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IRedemptionRepository _redemptions;
    private readonly TimeProvider _time;

    public ValidateCouponQueryHandler(ICouponRepository coupons, IRedemptionRepository redemptions, TimeProvider time)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _time = time;
    }

    public async Task<CouponQuoteResult> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var code = Coupon.NormalizeCode(request.Code);
        var coupon = await _coupons.GetByCodeAsync(code, ct)
            ?? throw new NotFoundAppException($"El cupón \"{code}\" no existe. Revisa que esté bien escrito.");

        var subtotal = decimal.Round(request.Subtotal, 2);
        var discount = await CouponEligibility.EnsureApplicableAsync(
            coupon, request.UserId, subtotal, _time.GetUtcNow().UtcDateTime, _redemptions, ct);

        return new CouponQuoteResult(coupon.Code, coupon.Description, subtotal, discount, subtotal - discount);
    }
}

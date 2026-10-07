using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Promotions.Application.Features;

/// <summary>Un cupón tal como lo ve el Admin, con cuántas veces se usó.</summary>
public record CouponAdminResult(
    Guid Id, string Code, string Description, string Type, decimal Value, decimal? MaxDiscountAmount,
    decimal MinimumSubtotal, DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, bool OncePerCustomer,
    bool IsActive, int TimesUsed, int ActiveReservations, DateTime CreatedAtUtc)
{
    public static CouponAdminResult From(Coupon c, CouponUsage usage) => new(
        c.Id, c.Code, c.Description, c.Type.ToString(), c.Value, c.MaxDiscountAmount, c.MinimumSubtotal,
        c.StartsAtUtc, c.EndsAtUtc, c.UsageLimit, c.OncePerCustomer, c.IsActive,
        usage.Confirmed, usage.ActiveReservations, c.CreatedAtUtc);
}

/// <summary>Los datos editables de un cupón (el código no cambia una vez creado).</summary>
public record CouponInput(
    string Description, DiscountType Type, decimal Value, decimal? MaxDiscountAmount, decimal MinimumSubtotal,
    DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, bool OncePerCustomer, bool IsActive = true);

// ---- Listar ----

public record ListCouponsQuery : IRequest<IReadOnlyList<CouponAdminResult>>;

public class ListCouponsQueryHandler : IRequestHandler<ListCouponsQuery, IReadOnlyList<CouponAdminResult>>
{
    private readonly ICouponRepository _coupons;
    private readonly IRedemptionRepository _redemptions;
    private readonly TimeProvider _time;

    public ListCouponsQueryHandler(ICouponRepository coupons, IRedemptionRepository redemptions, TimeProvider time)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _time = time;
    }

    public async Task<IReadOnlyList<CouponAdminResult>> Handle(ListCouponsQuery request, CancellationToken ct)
    {
        var coupons = await _coupons.ListAsync(ct);
        var usage = await _redemptions.GetUsageAsync(
            coupons.Select(c => c.Id).ToList(), _time.GetUtcNow().UtcDateTime, ct);

        return coupons
            .Select(c => CouponAdminResult.From(c, usage.GetValueOrDefault(c.Id, new CouponUsage(0, 0))))
            .ToList();
    }
}

// ---- Crear ----

public record CreateCouponCommand(string Code, CouponInput Data) : IRequest<CouponAdminResult>;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(Coupon.MaxCodeLength).WithMessage($"El código no puede superar {Coupon.MaxCodeLength} caracteres.");
        RuleFor(x => x.Data).NotNull().WithMessage("Faltan los datos del cupón.");
    }
}

public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, CouponAdminResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCouponCommandHandler(ICouponRepository coupons, IUnitOfWork unitOfWork)
    {
        _coupons = coupons;
        _unitOfWork = unitOfWork;
    }

    public async Task<CouponAdminResult> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var coupon = Coupon.Create(request.Code, d.Description, d.Type, d.Value, d.MaxDiscountAmount,
            d.MinimumSubtotal, d.StartsAtUtc, d.EndsAtUtc, d.UsageLimit, d.OncePerCustomer);

        if (!d.IsActive)
            coupon.Update(d.Description, d.Type, d.Value, d.MaxDiscountAmount, d.MinimumSubtotal,
                d.StartsAtUtc, d.EndsAtUtc, d.UsageLimit, d.OncePerCustomer, isActive: false);

        // Optimización de UX: la garantía real es el índice UNIQUE (ver UnitOfWork.SaveChangesAsync).
        if (await _coupons.CodeExistsAsync(coupon.Code, ct))
            throw new ConflictAppException($"Ya existe un cupón con el código \"{coupon.Code}\".");

        await _coupons.AddAsync(coupon, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return CouponAdminResult.From(coupon, new CouponUsage(0, 0));
    }
}

// ---- Editar ----

public record UpdateCouponCommand(Guid Id, CouponInput Data) : IRequest<CouponAdminResult>;

public class UpdateCouponCommandHandler : IRequestHandler<UpdateCouponCommand, CouponAdminResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public UpdateCouponCommandHandler(
        ICouponRepository coupons, IRedemptionRepository redemptions, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<CouponAdminResult> Handle(UpdateCouponCommand request, CancellationToken ct)
    {
        var coupon = await _coupons.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundAppException("El cupón no existe.");

        var d = request.Data;
        coupon.Update(d.Description, d.Type, d.Value, d.MaxDiscountAmount, d.MinimumSubtotal,
            d.StartsAtUtc, d.EndsAtUtc, d.UsageLimit, d.OncePerCustomer, d.IsActive);
        await _unitOfWork.SaveChangesAsync(ct);

        var usage = await _redemptions.GetUsageAsync(coupon.Id, _time.GetUtcNow().UtcDateTime, ct);
        return CouponAdminResult.From(coupon, usage);
    }
}

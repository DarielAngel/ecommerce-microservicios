using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Promotions.Application.Features;

public record RedemptionResult(Guid OrderId, string Code, decimal Subtotal, decimal DiscountAmount, string Status)
{
    public static RedemptionResult From(CouponRedemption r) =>
        new(r.OrderId, r.Code, r.Subtotal, r.DiscountAmount, r.Status.ToString());
}

// ---------------------------------------------------------------------------------------------
// Reservar (paso del checkout)
// ---------------------------------------------------------------------------------------------

/// <summary>Aparta un uso del cupón para una orden. Lo llama Órdenes durante el checkout.</summary>
public record ReserveRedemptionCommand(Guid OrderId, string Code, Guid UserId, decimal Subtotal) : IRequest<RedemptionResult>;

public class ReserveRedemptionCommandValidator : AbstractValidator<ReserveRedemptionCommand>
{
    public ReserveRedemptionCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("La orden es obligatoria.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código de cupón es obligatorio.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.Subtotal).GreaterThan(0).WithMessage("El subtotal debe ser mayor que 0.");
    }
}

public class ReserveRedemptionCommandHandler : IRequestHandler<ReserveRedemptionCommand, RedemptionResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public ReserveRedemptionCommandHandler(
        ICouponRepository coupons, IRedemptionRepository redemptions, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public Task<RedemptionResult> Handle(ReserveRedemptionCommand request, CancellationToken ct) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Idempotente: un reintento de la misma orden devuelve el canje que ya existe.
            var existing = await _redemptions.GetByOrderIdAsync(request.OrderId, ct);
            if (existing is not null)
            {
                if (existing.UserId != request.UserId)
                    throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");
                if (existing.Status == RedemptionStatus.Released)
                    throw new ConflictAppException("El cupón de esta orden ya se liberó; inicia un checkout nuevo.");
                return RedemptionResult.From(existing);
            }

            var code = Coupon.NormalizeCode(request.Code);

            // La fila del cupón queda bloqueada hasta el commit: dos checkouts a la vez se atienden
            // de a uno y el segundo ya ve el uso del primero al contar.
            var coupon = await _coupons.GetByCodeForUpdateAsync(code, ct)
                ?? throw new NotFoundAppException($"El cupón \"{code}\" no existe. Revisa que esté bien escrito.");

            var subtotal = decimal.Round(request.Subtotal, 2);
            var discount = await CouponEligibility.EnsureApplicableAsync(
                coupon, request.UserId, subtotal, _time.GetUtcNow().UtcDateTime, _redemptions, ct);

            var redemption = CouponRedemption.Reserve(request.OrderId, coupon, request.UserId, subtotal, discount);
            await _redemptions.AddAsync(redemption, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return RedemptionResult.From(redemption);
        }, ct);
}

// ---------------------------------------------------------------------------------------------
// Confirmar (pago capturado) y liberar (pago fallido / compensación)
// ---------------------------------------------------------------------------------------------

public record ConfirmRedemptionCommand(Guid OrderId, Guid RequesterId) : IRequest<RedemptionResult>;

public class ConfirmRedemptionCommandHandler : IRequestHandler<ConfirmRedemptionCommand, RedemptionResult>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmRedemptionCommandHandler(IRedemptionRepository redemptions, IUnitOfWork unitOfWork)
    {
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
    }

    public async Task<RedemptionResult> Handle(ConfirmRedemptionCommand request, CancellationToken ct)
    {
        var redemption = await _redemptions.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("Esa orden no tiene un cupón reservado.");

        if (redemption.UserId != request.RequesterId)
            throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");

        redemption.Confirm();
        await _unitOfWork.SaveChangesAsync(ct);
        return RedemptionResult.From(redemption);
    }
}

/// <summary>
/// Libera el uso apartado. Es una compensación de la saga: si la orden no tenía cupón, no hace nada
/// (Órdenes puede llamarla sin preguntar antes, y repetirla es seguro).
/// </summary>
public record ReleaseRedemptionCommand(Guid OrderId, Guid RequesterId) : IRequest;

public class ReleaseRedemptionCommandHandler : IRequestHandler<ReleaseRedemptionCommand>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;

    public ReleaseRedemptionCommandHandler(IRedemptionRepository redemptions, IUnitOfWork unitOfWork)
    {
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ReleaseRedemptionCommand request, CancellationToken ct)
    {
        var redemption = await _redemptions.GetByOrderIdAsync(request.OrderId, ct);
        if (redemption is null) return;

        if (redemption.UserId != request.RequesterId)
            throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");

        redemption.Release();
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

// ---------------------------------------------------------------------------------------------
// Devolver el uso (Fase 7): la compra se reembolsó entera
// ---------------------------------------------------------------------------------------------

/// <summary>
/// La orden se reembolsó completa (cancelada antes del envío o devuelta entera): su uso del cupón deja de contar.
/// Llega por el evento OrderRefundedEvent, así que tiene que ser idempotente y no fallar si la orden no tenía cupón.
/// </summary>
public record RestoreRedemptionCommand(Guid OrderId, Guid UserId) : IRequest<bool>;

public class RestoreRedemptionCommandHandler : IRequestHandler<RestoreRedemptionCommand, bool>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreRedemptionCommandHandler(IRedemptionRepository redemptions, IUnitOfWork unitOfWork)
    {
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
    }

    /// <returns>true si el uso se devolvió ahora; false si no había cupón o ya estaba devuelto.</returns>
    public async Task<bool> Handle(RestoreRedemptionCommand request, CancellationToken ct)
    {
        var redemption = await _redemptions.GetByOrderIdAsync(request.OrderId, ct);
        if (redemption is null) return false;

        // El evento viene de Órdenes, pero si el dueño no coincide algo está mal: no se toca nada.
        if (redemption.UserId != request.UserId) return false;

        if (!redemption.Restore()) return false;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

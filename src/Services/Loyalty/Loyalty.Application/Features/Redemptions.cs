using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Loyalty.Application.Features;

// Canje de puntos dentro de la saga de checkout de Órdenes. Igual que el stock y los cupones:
// reservar → confirmar (pago capturado) o liberar (pago fallido / compensación).

public record PointsRedemptionResult(Guid OrderId, int Points, decimal DiscountAmount, string Status)
{
    public static PointsRedemptionResult From(LoyaltyEntry e) => new(e.OrderId, e.Points, e.DiscountAmount, e.Status.ToString());
}

/// <summary>
/// Aparta todos los puntos que se pueden usar en una compra de <see cref="Amount"/> (lo que queda
/// después del cupón). El descuento lo calcula este servicio, no el navegador.
/// </summary>
public record ReservePointsCommand(Guid OrderId, Guid UserId, decimal Amount) : IRequest<PointsRedemptionResult>;

public class ReservePointsCommandValidator : AbstractValidator<ReservePointsCommand>
{
    public ReservePointsCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("La orden es obligatoria.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto de la compra debe ser mayor que 0.");
    }
}

public class ReservePointsCommandHandler : IRequestHandler<ReservePointsCommand, PointsRedemptionResult>
{
    private readonly ILoyaltyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public ReservePointsCommandHandler(ILoyaltyRepository repository, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public Task<PointsRedemptionResult> Handle(ReservePointsCommand request, CancellationToken ct) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // La cuenta queda bloqueada hasta el commit: dos checkouts a la vez del mismo cliente se
            // atienden de a uno, y el segundo ya ve los puntos que apartó el primero.
            await _repository.LockAccountAsync(request.UserId, ct);

            var existing = await _repository.GetAsync(request.OrderId, LoyaltyEntryKind.Redeemed, ct);
            if (existing is not null)
            {
                if (existing.UserId != request.UserId)
                    throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");
                if (existing.Status == LoyaltyEntryStatus.Released)
                    throw new ConflictAppException("Los puntos de esta orden ya se devolvieron; inicia un checkout nuevo.");

                // Órdenes vuelve a llamar acá justo antes de cobrar (renovación). Si el plazo de la reserva
                // venció, esos puntos dejaron de estar apartados y pudieron usarse en otra compra: se
                // re-verifica el saldo con la cuenta bloqueada. Si ya no alcanza, la compra no se cobra.
                var nowExisting = _time.GetUtcNow().UtcDateTime;
                if (existing.IsExpiredAt(nowExisting))
                {
                    var available = PointsLedger.Balance(await _repository.ListByUserAsync(request.UserId, ct), nowExisting);
                    if (available < existing.Points)
                        throw new ConflictAppException(
                            "Tus puntos ya no alcanzan para esta compra: se usaron en otra mientras esta esperaba el pago.");
                    existing.Renew(nowExisting);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                return PointsRedemptionResult.From(existing); // reintento o renovación de la misma orden
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var balance = PointsLedger.Balance(await _repository.ListByUserAsync(request.UserId, ct), now);
            var quote = LoyaltyRules.Quote(balance, decimal.Round(request.Amount, 2));
            if (quote.Points == 0)
            {
                throw new ConflictAppException(balance < LoyaltyRules.MinRedeemPoints
                    ? $"Necesitas al menos {LoyaltyRules.MinRedeemPoints} puntos para usarlos (tienes {balance})."
                    : "Esta compra es muy chica para usar puntos.");
            }

            var entry = LoyaltyEntry.Reserve(request.UserId, request.OrderId, quote, now);
            await _repository.AddAsync(entry, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return PointsRedemptionResult.From(entry);
        }, ct);
}

/// <summary>
/// <paramref name="RequesterId"/>: el cliente que llama (lo pasa el endpoint interno). Cuando confirma el
/// propio servicio al recibir OrderPaid, es null y se compara contra <paramref name="OwnerId"/> del evento.
/// </summary>
public record ConfirmPointsCommand(Guid OrderId, Guid? RequesterId, Guid? OwnerId = null) : IRequest<PointsRedemptionResult?>;

public class ConfirmPointsCommandHandler : IRequestHandler<ConfirmPointsCommand, PointsRedemptionResult?>
{
    private readonly ILoyaltyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public ConfirmPointsCommandHandler(ILoyaltyRepository repository, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<PointsRedemptionResult?> Handle(ConfirmPointsCommand request, CancellationToken ct)
    {
        var entry = await _repository.GetAsync(request.OrderId, LoyaltyEntryKind.Redeemed, ct);
        if (entry is null)
        {
            // Desde el evento OrderPaid: la mayoría de las órdenes no usan puntos.
            if (request.RequesterId is null) return null;
            throw new NotFoundAppException("Esa orden no tiene puntos apartados.");
        }

        var caller = request.RequesterId ?? request.OwnerId;
        if (entry.UserId != caller)
            throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");

        // El pago ya se cobró con el descuento: el canje queda firme aunque su plazo haya vencido.
        entry.Confirm(_time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(ct);
        return PointsRedemptionResult.From(entry);
    }
}

/// <summary>Compensación: si la orden no usó puntos no hace nada, y repetirla es seguro.</summary>
public record ReleasePointsCommand(Guid OrderId, Guid RequesterId) : IRequest;

public class ReleasePointsCommandHandler : IRequestHandler<ReleasePointsCommand>
{
    private readonly ILoyaltyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public ReleasePointsCommandHandler(ILoyaltyRepository repository, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task Handle(ReleasePointsCommand request, CancellationToken ct)
    {
        var entry = await _repository.GetAsync(request.OrderId, LoyaltyEntryKind.Redeemed, ct);
        if (entry is null) return;
        if (entry.UserId != request.RequesterId)
            throw new ForbiddenAppException("Esa orden pertenece a otro cliente.");

        entry.Release(_time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

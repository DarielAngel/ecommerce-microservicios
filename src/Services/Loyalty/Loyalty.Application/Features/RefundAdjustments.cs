using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Loyalty.Application.Features;

/// <summary>
/// Ajusta los puntos cuando una devolución se reembolsa (evento OrderRefunded, Fase 7):
/// <list type="bullet">
/// <item>descuenta los puntos que esa parte de la compra había dado (1 por cada $1 devuelto; si con esto se
/// devolvió todo el pedido, todo lo que quedaba de lo ganado);</item>
/// <item>devuelve <paramref name="PointsToRestore"/> de los que el cliente había usado (los calcula Órdenes, que
/// sabe cuántos puntos usó cada pedido).</item>
/// </list>
/// Nunca descuenta más de lo que esa compra dio (ni más que el saldo actual) ni devuelve más de lo que usó, y cada
/// devolución se aplica una sola vez.
/// </summary>
public record ApplyRefundCommand(
    Guid UserId, Guid OrderId, Guid ReturnId, decimal RefundAmount, bool OrderFullyRefunded, int PointsToRestore) : IRequest;

public class ApplyRefundCommandHandler : IRequestHandler<ApplyRefundCommand>
{
    private readonly ILoyaltyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<ApplyRefundCommandHandler> _logger;

    public ApplyRefundCommandHandler(
        ILoyaltyRepository repository, IUnitOfWork unitOfWork, TimeProvider time, ILogger<ApplyRefundCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    public async Task Handle(ApplyRefundCommand request, CancellationToken ct)
    {
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // La cuenta bloqueada: dos devoluciones del mismo cliente a la vez no calculan sobre el mismo "ya descontado".
                await _repository.LockAccountAsync(request.UserId, ct);
                var entries = await _repository.ListByOrderAsync(request.OrderId, ct);

                if (entries.Any(e => e.ReferenceId == request.ReturnId))
                {
                    _logger.LogInformation("La devolución {ReturnId} ya había ajustado los puntos — se ignora el duplicado.", request.ReturnId);
                    return 0;
                }

                var now = _time.GetUtcNow().UtcDateTime;
                var earned = entries.Where(e => e.Kind == LoyaltyEntryKind.Earned).Sum(e => e.Points);
                var reversed = entries.Where(e => e.Kind == LoyaltyEntryKind.Reversed).Sum(e => e.Points);
                var toReverse = Math.Max(0, request.OrderFullyRefunded
                    ? earned - reversed
                    : Math.Min(LoyaltyRules.PointsEarnedFor(request.RefundAmount), earned - reversed));

                var used = entries.Where(e => e.Kind == LoyaltyEntryKind.Redeemed && e.Status == LoyaltyEntryStatus.Confirmed).Sum(e => e.Points);
                var restored = entries.Where(e => e.Kind == LoyaltyEntryKind.Restored).Sum(e => e.Points);
                var toRestore = Math.Max(0, Math.Min(request.PointsToRestore, used - restored));

                // No se descuenta más de lo que el cliente tiene: si ya gastó los puntos de esta compra, no queda
                // "debiendo" (si no, las compras siguientes no le sumarían nada hasta cubrir la deuda).
                var balance = PointsLedger.Balance(await _repository.ListByUserAsync(request.UserId, ct), now) + toRestore;
                toReverse = Math.Min(toReverse, balance);

                if (toReverse > 0)
                    await _repository.AddAsync(LoyaltyEntry.ForRefund(request.UserId, request.OrderId, request.ReturnId, LoyaltyEntryKind.Reversed, toReverse, now), ct);
                if (toRestore > 0)
                    await _repository.AddAsync(LoyaltyEntry.ForRefund(request.UserId, request.OrderId, request.ReturnId, LoyaltyEntryKind.Restored, toRestore, now), ct);

                await _unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation("Devolución {ReturnId}: −{Reversed} puntos ganados, +{Restored} puntos usados.",
                    request.ReturnId, toReverse, toRestore);
                return 0;
            }, ct);
        }
        catch (ConflictAppException)
        {
            // Dos copias del evento a la vez: el índice único dejó pasar solo una. Está bien.
            _logger.LogInformation("Otra copia del evento ya ajustó los puntos de la devolución {ReturnId}.", request.ReturnId);
        }
    }
}

using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Loyalty.Application.Features;

public record LoyaltyRulesResult(int PointsPerDollar, decimal PointValue, int MinRedeemPoints, decimal MaxRedeemShare)
{
    public static LoyaltyRulesResult Current { get; } = new(
        LoyaltyRules.PointsPerDollar, LoyaltyRules.PointValue, LoyaltyRules.MinRedeemPoints, LoyaltyRules.MaxRedeemShare);
}

/// <param name="ReferenceId">En los ajustes por devolución (Reversed / Restored), el id de la devolución.</param>
public record LoyaltyEntryResult(Guid OrderId, string Kind, int Points, decimal DiscountAmount, string Status, DateTime CreatedAtUtc,
    Guid? ReferenceId = null)
{
    public static LoyaltyEntryResult From(LoyaltyEntry e) =>
        new(e.OrderId, e.Kind.ToString(), e.Points, e.DiscountAmount, e.Status.ToString(), e.CreatedAtUtc,
            e.ReferenceId == Guid.Empty ? null : e.ReferenceId);
}

public record LoyaltySummaryResult(int Balance, decimal BalanceValue, LoyaltyRulesResult Rules, IReadOnlyList<LoyaltyEntryResult> History);

// ---------- Mi saldo e historial ----------

public record GetMyPointsQuery(Guid UserId, int HistoryLimit = 30) : IRequest<LoyaltySummaryResult>;

public class GetMyPointsQueryHandler : IRequestHandler<GetMyPointsQuery, LoyaltySummaryResult>
{
    private readonly ILoyaltyRepository _repository;
    private readonly TimeProvider _time;

    public GetMyPointsQueryHandler(ILoyaltyRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
    }

    public async Task<LoyaltySummaryResult> Handle(GetMyPointsQuery request, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var entries = await _repository.ListByUserAsync(request.UserId, ct);
        var balance = PointsLedger.Balance(entries, now);

        // En el historial no mostramos canjes deshechos ni apartados que vencieron (checkouts que no se
        // pagaron): para el cliente no pasaron, y así la lista coincide con el saldo.
        var history = entries
            .Where(e => e.Status != LoyaltyEntryStatus.Released && !e.IsExpiredAt(now))
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(Math.Clamp(request.HistoryLimit, 1, 100))
            .Select(LoyaltyEntryResult.From)
            .ToList();

        return new LoyaltySummaryResult(balance, LoyaltyRules.ValueOf(balance), LoyaltyRulesResult.Current, history);
    }
}

// ---------- ¿Cuánto me descuentan mis puntos en esta compra? ----------

public record QuoteRedemptionQuery(Guid UserId, decimal Amount) : IRequest<QuoteResult>;

public record QuoteResult(int Balance, int Points, decimal Discount);

public class QuoteRedemptionQueryValidator : AbstractValidator<QuoteRedemptionQuery>
{
    public QuoteRedemptionQueryValidator() =>
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("El monto no puede ser negativo.");
}

public class QuoteRedemptionQueryHandler : IRequestHandler<QuoteRedemptionQuery, QuoteResult>
{
    private readonly ILoyaltyRepository _repository;
    private readonly TimeProvider _time;

    public QuoteRedemptionQueryHandler(ILoyaltyRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
    }

    public async Task<QuoteResult> Handle(QuoteRedemptionQuery request, CancellationToken ct)
    {
        var balance = PointsLedger.Balance(await _repository.ListByUserAsync(request.UserId, ct), _time.GetUtcNow().UtcDateTime);
        var quote = LoyaltyRules.Quote(balance, decimal.Round(request.Amount, 2));
        return new QuoteResult(balance, quote.Points, quote.Discount);
    }
}

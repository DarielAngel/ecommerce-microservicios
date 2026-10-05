using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

// ---- Un producto ----
public record GetRatingSummaryQuery(Guid ProductId) : IRequest<RatingSummaryResult>;

public class GetRatingSummaryQueryHandler : IRequestHandler<GetRatingSummaryQuery, RatingSummaryResult>
{
    private readonly IReviewRepository _reviews;

    public GetRatingSummaryQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<RatingSummaryResult> Handle(GetRatingSummaryQuery request, CancellationToken ct)
    {
        var distribution = await _reviews.GetRatingDistributionAsync(request.ProductId, ct);
        return RatingSummaryResult.From(RatingSummary.FromDistribution(request.ProductId, distribution));
    }
}

// ---- Varios productos en una sola llamada (para pintar las estrellas de toda una grilla) ----
public record GetRatingSummariesQuery(IReadOnlyCollection<Guid> ProductIds) : IRequest<IReadOnlyList<RatingSummaryResult>>;

public class GetRatingSummariesQueryValidator : AbstractValidator<GetRatingSummariesQuery>
{
    public const int MaxProducts = 100;

    public GetRatingSummariesQueryValidator()
    {
        RuleFor(x => x.ProductIds)
            .Must(ids => ids.Count <= MaxProducts)
            .WithMessage($"Se pueden consultar hasta {MaxProducts} productos por llamada.");
    }
}

public class GetRatingSummariesQueryHandler : IRequestHandler<GetRatingSummariesQuery, IReadOnlyList<RatingSummaryResult>>
{
    private readonly IReviewRepository _reviews;

    public GetRatingSummariesQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<IReadOnlyList<RatingSummaryResult>> Handle(GetRatingSummariesQuery request, CancellationToken ct)
    {
        if (request.ProductIds.Count == 0) return Array.Empty<RatingSummaryResult>();

        var ids = request.ProductIds.Distinct().ToList();
        var distributions = await _reviews.GetRatingDistributionsAsync(ids, ct);

        // Una entrada por cada producto pedido, con ceros para los que aún no tienen reseñas:
        // así el frontend no tiene que distinguir "sin datos" de "sin reseñas".
        return ids
            .Select(id => RatingSummaryResult.From(RatingSummary.FromDistribution(
                id, distributions.TryGetValue(id, out var distribution) ? distribution : new Dictionary<int, int>())))
            .ToList();
    }
}

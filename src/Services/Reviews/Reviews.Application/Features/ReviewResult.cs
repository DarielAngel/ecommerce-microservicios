using Ecommerce.Reviews.Domain.Entities;
using Ecommerce.Reviews.Domain.ValueObjects;

namespace Ecommerce.Reviews.Application.Features;

/// <summary>
/// Reseña tal como la ve el público. A propósito NO incluye el id del usuario: la API de lectura es
/// pública y no debe filtrar identificadores. El cliente conoce SU reseña vía GET .../mine.
/// </summary>
public record ReviewResult(
    Guid Id,
    Guid ProductId,
    string AuthorName,
    int Rating,
    string Title,
    string Comment,
    bool IsVerifiedPurchase,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ReviewResult From(Review r) => new(
        r.Id, r.ProductId, r.AuthorName, r.Rating, r.Title, r.Comment,
        r.IsVerifiedPurchase, r.CreatedAtUtc, r.UpdatedAtUtc);
}

public record RatingSummaryResult(Guid ProductId, double Average, int Count, IReadOnlyDictionary<int, int> Distribution)
{
    public static RatingSummaryResult From(RatingSummary s) => new(s.ProductId, s.Average, s.Count, s.Distribution);
}

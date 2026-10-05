using Ecommerce.Reviews.Domain.Entities;

namespace Ecommerce.Reviews.Domain.ValueObjects;

/// <summary>Promedio, cantidad y distribución por estrellas de las reseñas de un producto.</summary>
public sealed record RatingSummary(Guid ProductId, double Average, int Count, IReadOnlyDictionary<int, int> Distribution)
{
    /// <summary>
    /// Calcula todo a partir de la distribución cruda (estrella -> cantidad). Siempre devuelve las cinco
    /// estrellas (con 0 las que no llegaron) y descarta valores fuera de 1..5.
    /// </summary>
    public static RatingSummary FromDistribution(Guid productId, IReadOnlyDictionary<int, int> raw)
    {
        var distribution = Enumerable
            .Range(Review.MinRating, Review.MaxRating - Review.MinRating + 1)
            .ToDictionary(star => star, star => raw.TryGetValue(star, out var count) ? Math.Max(count, 0) : 0);

        var total = distribution.Values.Sum();
        var average = total == 0
            ? 0
            : Math.Round(distribution.Sum(kv => kv.Key * kv.Value) / (double)total, 1, MidpointRounding.AwayFromZero);

        return new RatingSummary(productId, average, total, distribution);
    }
}

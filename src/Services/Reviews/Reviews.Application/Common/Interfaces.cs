using Ecommerce.Reviews.Domain.Entities;

namespace Ecommerce.Reviews.Application.Common;

public enum ReviewSort
{
    Newest,
    Highest,
    Lowest
}

/// <summary>Una página de reseñas ya ordenada, más el total para calcular el número de páginas.</summary>
public record ReviewPage(IReadOnlyList<Review> Items, int TotalCount);

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Review?> GetByUserAndProductAsync(Guid userId, Guid productId, CancellationToken ct);
    Task AddAsync(Review review, CancellationToken ct);
    void Remove(Review review);

    Task<ReviewPage> ListByProductAsync(Guid productId, ReviewSort sort, int page, int pageSize, CancellationToken ct);

    /// <summary>Estrella -> cantidad de reseñas. Solo trae las estrellas que existen.</summary>
    Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(Guid productId, CancellationToken ct);

    /// <summary>Lo mismo para varios productos a la vez (una sola consulta). Los productos sin reseñas no aparecen.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, int>>> GetRatingDistributionsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct);

    /// <summary>Marca como "compra verificada" las reseñas que este usuario ya había escrito de esos productos.</summary>
    Task MarkVerifiedAsync(Guid userId, IReadOnlyCollection<Guid> productIds, CancellationToken ct);

    /// <summary>Guarda los cambios. Traduce la violación de unicidad (usuario, producto) a ConflictAppException.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IVerifiedPurchaseRepository
{
    Task<bool> HasPurchasedAsync(Guid userId, Guid productId, CancellationToken ct);

    /// <summary>Registra las compras de forma idempotente: repetir el mismo evento no duplica ni falla.</summary>
    Task RecordAsync(Guid userId, IReadOnlyCollection<Guid> productIds, CancellationToken ct);
}

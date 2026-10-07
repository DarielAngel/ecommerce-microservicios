using Ecommerce.Wishlist.Domain.Entities;

namespace Ecommerce.Wishlist.Application.Common;

public interface IWishlistRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid productId, CancellationToken ct);

    Task<int> CountAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Inserta el favorito si no existe, de forma atómica (INSERT ... ON CONFLICT DO NOTHING).
    /// Dos clics simultáneos en el corazón nunca fallan ni duplican: gana uno y el otro no hace nada.
    /// </summary>
    Task AddIfMissingAsync(WishlistItem item, CancellationToken ct);

    /// <summary>Quita el favorito. Si no estaba, no hace nada (idempotente).</summary>
    Task RemoveAsync(Guid userId, Guid productId, CancellationToken ct);

    /// <summary>Los favoritos del usuario, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<WishlistItem>> ListAsync(Guid userId, CancellationToken ct);
}

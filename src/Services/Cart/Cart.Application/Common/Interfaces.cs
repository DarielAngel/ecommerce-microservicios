using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;

namespace Ecommerce.Cart.Application.Common;

public interface ICartRepository
{
    Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(CartAggregate cart, CancellationToken ct);

    /// <summary>Marca explícitamente un CartItem recién agregado a un carrito YA CARGADO como "Added".</summary>
    void TrackNewItem(Domain.Entities.CartItem item);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Carritos con productos cuya última modificación cae entre <paramref name="updatedFrom"/> y
    /// <paramref name="updatedUntil"/>, con sus ítems (candidatos a recordatorio de carrito abandonado).
    /// </summary>
    Task<IReadOnlyList<CartAggregate>> ListIdleAsync(DateTime updatedFrom, DateTime updatedUntil, int limit, CancellationToken ct);
}

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class;
}

public record VariantInfo(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal Price, bool IsActive);

/// <summary>Cliente HTTP hacia el microservicio de Catálogo (llamada pública, sin token).</summary>
public interface ICatalogServiceClient
{
    Task<VariantInfo?> GetVariantAsync(Guid variantId, CancellationToken ct);
}

/// <summary>
/// Cliente HTTP hacia el microservicio de Inventario. Requiere un token de acceso: se reenvía
/// el mismo JWT con el que el usuario llamó a Carrito (Inventario exige usuario autenticado).
/// </summary>
public interface IInventoryServiceClient
{
    Task<int> GetAvailableQuantityAsync(Guid variantId, string accessToken, CancellationToken ct);
}

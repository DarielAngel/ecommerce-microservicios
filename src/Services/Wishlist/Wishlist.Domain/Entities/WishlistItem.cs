using Ecommerce.Wishlist.Domain.Exceptions;

namespace Ecommerce.Wishlist.Domain.Entities;

/// <summary>
/// Un producto marcado como favorito por un cliente. La clave es (usuario, producto): un producto
/// aparece una sola vez en la lista de cada cliente, y eso lo garantiza la clave primaria de la tabla.
/// A propósito guardamos solo el id del producto, no una copia de su nombre o precio: así la lista
/// siempre muestra los datos actuales del catálogo y no hay nada que se quede desactualizado.
/// </summary>
public class WishlistItem
{
    /// <summary>Tope por cliente: una lista de favoritos es para guardar, no un segundo catálogo.</summary>
    public const int MaxItemsPerUser = 100;

    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    private WishlistItem() { }

    public static WishlistItem Create(Guid userId, Guid productId)
    {
        if (userId == Guid.Empty) throw new DomainException("El usuario es obligatorio.");
        if (productId == Guid.Empty) throw new DomainException("El producto es obligatorio.");

        return new WishlistItem
        {
            UserId = userId,
            ProductId = productId,
            AddedAtUtc = DateTime.UtcNow
        };
    }
}

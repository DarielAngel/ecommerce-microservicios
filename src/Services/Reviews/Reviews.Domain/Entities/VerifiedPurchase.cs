namespace Ecommerce.Reviews.Domain.Entities;

/// <summary>
/// Hecho "este usuario compró (y pagó) este producto", alimentado por el evento OrderPaid. Vive en la
/// base de Reseñas a propósito: cada servicio es dueño de sus datos y no consulta tablas de Órdenes.
/// </summary>
public class VerifiedPurchase
{
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime FirstPurchasedAtUtc { get; private set; }

    private VerifiedPurchase() { }

    public VerifiedPurchase(Guid userId, Guid productId, DateTime firstPurchasedAtUtc)
    {
        UserId = userId;
        ProductId = productId;
        FirstPurchasedAtUtc = firstPurchasedAtUtc;
    }
}

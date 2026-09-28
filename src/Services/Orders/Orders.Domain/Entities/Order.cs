using Ecommerce.Orders.Domain.Enums;
using Ecommerce.Orders.Domain.Exceptions;

namespace Ecommerce.Orders.Domain.Entities;

public class Order
{
    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string UserEmail { get; private set; } = null!;
    public string UserFullName { get; private set; } = null!;
    public string ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }

    public decimal TotalAmount => _lines.Sum(l => l.LineTotal);

    private Order() { }

    private Order(Guid id, Guid userId, string userEmail, string userFullName, string shippingAddress)
    {
        Id = id;
        UserId = userId;
        UserEmail = userEmail;
        UserFullName = userFullName;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.PendingPayment;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Crea la orden ya con sus líneas (checkout parcial: el caller decide qué ítems del
    /// carrito entran). La orden nace directamente en PendingPayment porque, para cuando se
    /// llama a este método, el stock YA se reservó con éxito (ver CheckoutCommandHandler) —
    /// no tendría sentido un estado "Created" transitorio que nadie observa.
    ///
    /// IMPORTANTE: recibe el `orderId` desde afuera (en vez de generarlo internamente) porque
    /// el caller ya tuvo que generarlo ANTES de llamar a este método, para poder reservar el
    /// stock en Inventario usándolo como clave de la reserva. Si generáramos el Id acá adentro,
    /// terminaríamos con dos Ids distintos para la misma orden — el de la reserva de stock y el
    /// de la orden persistida — y ConfirmPayment no podría confirmar/liberar la reserva correcta.
    ///
    /// Guarda también el email/nombre del comprador (tomados del JWT al momento del checkout)
    /// para no tener que volver a consultar a Users cuando haya que mandar notificaciones.
    /// </summary>
    public static Order Create(
        Guid orderId,
        Guid userId,
        string userEmail,
        string userFullName,
        string shippingAddress,
        IEnumerable<(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity)> items)
    {
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            throw new DomainException("La dirección de envío es obligatoria.");
        }

        var itemsList = items.ToList();

        if (itemsList.Count == 0)
        {
            throw new DomainException("La orden necesita al menos un ítem.");
        }

        if (itemsList.Any(i => i.Quantity <= 0))
        {
            throw new DomainException("Todas las cantidades deben ser mayores a cero.");
        }

        var order = new Order(orderId, userId, userEmail, userFullName, shippingAddress.Trim());

        foreach (var item in itemsList)
        {
            order._lines.Add(new OrderLine(
                item.VariantId, item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity));
        }

        return order;
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException($"No se puede marcar como pagada una orden en estado '{Status}'.");
        }

        Status = OrderStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("No se puede marcar como fallida una orden que ya fue pagada.");
        }

        Status = OrderStatus.Failed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("No se puede cancelar una orden que ya fue pagada.");
        }

        Status = OrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkShipped()
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException($"Solo se puede marcar como enviada una orden pagada (estado actual: '{Status}').");
        }

        Status = OrderStatus.Shipped;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

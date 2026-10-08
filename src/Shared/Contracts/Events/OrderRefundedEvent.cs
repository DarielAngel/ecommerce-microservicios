namespace Ecommerce.Contracts.Events;

/// <summary>
/// Órdenes lo publica cuando una devolución quedó reembolsada en PayPal (Fase 7).
/// <list type="bullet">
/// <item>Inventario vuelve a sumar al stock las unidades devueltas.</item>
/// <item>Lealtad descuenta los puntos que esa parte de la compra había dado y devuelve
/// <see cref="LoyaltyPointsToRestore"/> de los que el cliente había usado.</item>
/// <item>Notificaciones le avisa al cliente.</item>
/// </list>
/// <see cref="ReturnId"/> identifica la devolución: los consumidores lo usan para no aplicar dos veces el mismo
/// evento (RabbitMQ entrega "al menos una vez").
/// </summary>
public record OrderRefundedEvent(
    Guid ReturnId,
    Guid OrderId,
    Guid UserId,
    string Email,
    string FullName,
    decimal RefundAmount,
    string Currency,
    bool OrderFullyRefunded,
    int LoyaltyPointsToRestore,
    IReadOnlyList<RefundedItem> Items,
    DateTime RefundedAtUtc);

public record RefundedItem(Guid VariantId, Guid ProductId, string ProductName, int Quantity);

/// <summary>El Admin rechazó una solicitud de devolución; Notificaciones le avisa al cliente con la nota.</summary>
public record ReturnRejectedEvent(
    Guid ReturnId,
    Guid OrderId,
    Guid UserId,
    string Email,
    string FullName,
    string Note,
    DateTime RejectedAtUtc);

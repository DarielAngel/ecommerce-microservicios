namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Carrito cuando un cliente deja productos en su carrito sin comprarlos durante un rato
/// (Fase 6, T6.2). Notificaciones lo consume para mandar UN correo de recordatorio.
/// </summary>
/// <param name="ReminderId">
/// Id único de ESTE recordatorio. Si RabbitMQ entrega el evento dos veces, Notificaciones lo usa para no
/// mandar el correo dos veces; si el cliente vuelve a abandonar el carrito más adelante, es otro id.
/// </param>
public record CartAbandonedEvent(
    Guid ReminderId,
    Guid CartId,
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<AbandonedCartItem> Items,
    decimal Subtotal,
    DateTime LastActivityAtUtc,
    DateTime OccurredAtUtc);

public record AbandonedCartItem(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

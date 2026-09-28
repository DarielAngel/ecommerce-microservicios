namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Órdenes (vía RabbitMQ) cuando una orden queda pagada (después de capturar
/// el pago y confirmar el stock). Notificaciones lo consume para mandar el email de confirmación.
/// </summary>
public record OrderPaidEvent(
    Guid OrderId,
    Guid UserId,
    string Email,
    string FullName,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredAtUtc);

namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Órdenes (vía RabbitMQ) cuando un Admin marca una orden como enviada.
/// Notificaciones lo consume para avisarle al cliente que su pedido va en camino.
/// </summary>
public record OrderShippedEvent(
    Guid OrderId,
    Guid UserId,
    string Email,
    string FullName,
    DateTime OccurredAtUtc);

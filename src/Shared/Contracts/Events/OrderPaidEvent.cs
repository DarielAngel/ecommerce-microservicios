namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Órdenes (vía RabbitMQ) cuando una orden queda pagada (después de capturar
/// el pago y confirmar el stock).
/// <list type="bullet">
///   <item>Notificaciones lo consume para mandar el email de confirmación.</item>
///   <item>Reseñas lo consume para marcar como "compra verificada" a quien compró cada producto.</item>
/// </list>
/// </summary>
/// <param name="ProductIds">
/// Ids de producto comprados, sin repetir. Es OPCIONAL y va al final a propósito: los contratos solo se
/// extienden, nunca se rompen — los consumidores que no lo usan (o eventos publicados antes de este
/// campo, que llegan con null) siguen funcionando igual.
/// </param>
public record OrderPaidEvent(
    Guid OrderId,
    Guid UserId,
    string Email,
    string FullName,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredAtUtc,
    IReadOnlyList<Guid>? ProductIds = null);

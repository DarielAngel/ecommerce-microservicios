namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Users (vía RabbitMQ) cada vez que se registra un cliente nuevo.
/// Notificaciones lo consume para mandar el email de bienvenida.
/// </summary>
public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FullName,
    DateTime OccurredAtUtc);

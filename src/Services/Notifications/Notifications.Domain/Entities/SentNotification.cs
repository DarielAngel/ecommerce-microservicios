using Ecommerce.Notifications.Domain.Enums;

namespace Ecommerce.Notifications.Domain.Entities;

/// <summary>
/// Registro de un email ya enviado. Sirve para la IDEMPOTENCIA: RabbitMQ garantiza entrega
/// "al menos una vez", así que un mismo evento puede llegar duplicado — con este registro
/// (único por tipo + id de referencia) nunca le mandamos dos veces el mismo email al cliente.
/// </summary>
public class SentNotification
{
    public Guid Id { get; private set; }
    public NotificationType Type { get; private set; }

    /// <summary>UserId (UserRegistered), OrderId (OrderPaid/OrderShipped) o el id del recordatorio (CartAbandoned).</summary>
    public Guid ReferenceId { get; private set; }

    public string RecipientEmail { get; private set; } = null!;
    public DateTime SentAtUtc { get; private set; }

    private SentNotification() { }

    public static SentNotification Create(NotificationType type, Guid referenceId, string recipientEmail) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        ReferenceId = referenceId,
        RecipientEmail = recipientEmail,
        SentAtUtc = DateTime.UtcNow
    };
}

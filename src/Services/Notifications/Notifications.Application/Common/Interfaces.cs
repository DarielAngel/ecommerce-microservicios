using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;

namespace Ecommerce.Notifications.Application.Common;

public interface INotificationLogRepository
{
    Task<bool> ExistsAsync(NotificationType type, Guid referenceId, CancellationToken ct);

    /// <summary>
    /// Registra el envío. Devuelve false (sin lanzar) si ya existía uno con el mismo tipo +
    /// referencia: eso significa que otro proceso/reintento ya lo registró (evento duplicado).
    /// </summary>
    Task<bool> TryAddAsync(SentNotification notification, CancellationToken ct);

    Task<IReadOnlyList<SentNotification>> ListRecentAsync(int count, CancellationToken ct);
}

/// <summary>Abstrae el proveedor real de email (Resend hoy; podría ser SendGrid/SMTP mañana).</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}

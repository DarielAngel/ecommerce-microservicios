using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Notifications.Application.Common;

/// <summary>
/// Lógica común de los 3 casos de uso: (1) si ya se envió, no repetir; (2) enviar el email;
/// (3) registrar el envío. Si el envío FALLA, la excepción sube y NO se registra nada — así
/// MassTransit reintenta el mensaje más tarde y el cliente sí termina recibiendo su email.
/// </summary>
public class NotificationDispatcher
{
    private readonly INotificationLogRepository _log;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationLogRepository log, IEmailSender emailSender, ILogger<NotificationDispatcher> logger)
    {
        _log = log;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task DispatchAsync(
        NotificationType type, Guid referenceId, string toEmail, string subject, string html, CancellationToken ct)
    {
        if (await _log.ExistsAsync(type, referenceId, ct))
        {
            _logger.LogInformation("Notificación {Type} para {ReferenceId} ya enviada — se ignora el duplicado.", type, referenceId);
            return;
        }

        await _emailSender.SendAsync(toEmail, subject, html, ct);

        var registered = await _log.TryAddAsync(SentNotification.Create(type, referenceId, toEmail), ct);

        if (!registered)
        {
            _logger.LogWarning(
                "Notificación {Type} para {ReferenceId} enviada, pero otro proceso ya la había registrado (carrera entre duplicados).",
                type, referenceId);
        }
    }
}

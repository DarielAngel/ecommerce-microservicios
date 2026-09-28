using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Notifications.Application.Common;

/// <summary>
/// Lógica común de los 3 casos de uso, con el orden "reservar → enviar → (deshacer si falla)":
///  1. Si ya existe el registro, es un duplicado: no se hace nada.
///  2. Se "reserva" el registro ANTES de enviar. La restricción única de la base de datos
///     garantiza que, si dos copias del mismo evento se procesan a la vez, solo una gana la
///     reserva — la otra sale sin enviar. Así nunca hay emails duplicados, ni con concurrencia.
///  3. Se envía el email. Si FALLA, se deshace la reserva y la excepción sube: MassTransit
///     reintenta el mensaje y el cliente sí termina recibiendo su email.
/// (Compromiso conocido: si el proceso muere justo entre el paso 2 y 3, ese email se pierde.
///  Para emails transaccionales preferimos "nunca duplicar" sobre "nunca perder en un crash".)
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

        var reserved = await _log.TryAddAsync(SentNotification.Create(type, referenceId, toEmail), ct);

        if (!reserved)
        {
            _logger.LogInformation(
                "Notificación {Type} para {ReferenceId} ya la está procesando otra copia del evento — se ignora.", type, referenceId);
            return;
        }

        try
        {
            await _emailSender.SendAsync(toEmail, subject, html, ct);
        }
        catch
        {
            // Compensación: liberar la reserva para que el reintento pueda volver a intentar el envío.
            // CancellationToken.None a propósito: aunque la solicitud se cancele, el rollback debe ocurrir.
            await _log.RemoveAsync(type, referenceId, CancellationToken.None);
            throw;
        }
    }
}

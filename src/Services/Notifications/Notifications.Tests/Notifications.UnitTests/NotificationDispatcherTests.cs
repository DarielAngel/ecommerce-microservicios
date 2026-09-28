using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecommerce.Notifications.UnitTests;

public class NotificationDispatcherTests
{
    private readonly INotificationLogRepository _log = Substitute.For<INotificationLogRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    private NotificationDispatcher CreateDispatcher() =>
        new(_log, _emailSender, NullLogger<NotificationDispatcher>.Instance);

    [Fact]
    public async Task Dispatch_ConNotificacionNueva_DeberiaReservarYEnviar()
    {
        var referenceId = Guid.NewGuid();
        _log.ExistsAsync(NotificationType.OrderPaid, referenceId, Arg.Any<CancellationToken>()).Returns(false);
        _log.TryAddAsync(Arg.Any<SentNotification>(), Arg.Any<CancellationToken>()).Returns(true);

        await CreateDispatcher().DispatchAsync(NotificationType.OrderPaid, referenceId, "a@test.com", "Asunto", "<p>x</p>", CancellationToken.None);

        await _emailSender.Received(1).SendAsync("a@test.com", "Asunto", "<p>x</p>", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_ConNotificacionYaEnviada_NoDeberiaEnviarNada()
    {
        var referenceId = Guid.NewGuid();
        _log.ExistsAsync(NotificationType.OrderPaid, referenceId, Arg.Any<CancellationToken>()).Returns(true);

        await CreateDispatcher().DispatchAsync(NotificationType.OrderPaid, referenceId, "a@test.com", "Asunto", "<p>x</p>", CancellationToken.None);

        await _emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _log.DidNotReceive().TryAddAsync(Arg.Any<SentNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_SiOtraCopiaGanoLaReserva_NoDeberiaEnviar()
    {
        var referenceId = Guid.NewGuid();
        _log.ExistsAsync(Arg.Any<NotificationType>(), referenceId, Arg.Any<CancellationToken>()).Returns(false);
        _log.TryAddAsync(Arg.Any<SentNotification>(), Arg.Any<CancellationToken>()).Returns(false); // perdió la carrera

        await CreateDispatcher().DispatchAsync(NotificationType.UserRegistered, referenceId, "a@test.com", "Asunto", "<p>x</p>", CancellationToken.None);

        await _emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_SiElEnvioFalla_DeberiaDeshacerLaReservaYPropagarLaExcepcion()
    {
        var referenceId = Guid.NewGuid();
        _log.ExistsAsync(Arg.Any<NotificationType>(), referenceId, Arg.Any<CancellationToken>()).Returns(false);
        _log.TryAddAsync(Arg.Any<SentNotification>(), Arg.Any<CancellationToken>()).Returns(true);
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EmailSendException("Resend caído"));

        var act = async () => await CreateDispatcher().DispatchAsync(
            NotificationType.OrderShipped, referenceId, "a@test.com", "Asunto", "<p>x</p>", CancellationToken.None);

        await act.Should().ThrowAsync<EmailSendException>();

        // Clave para los reintentos: sin deshacer la reserva, el reintento creería que ya se envió.
        await _log.Received(1).RemoveAsync(NotificationType.OrderShipped, referenceId, Arg.Any<CancellationToken>());
    }
}

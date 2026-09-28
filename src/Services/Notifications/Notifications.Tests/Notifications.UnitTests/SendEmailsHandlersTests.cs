using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Application.Features;
using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Notifications.UnitTests;

public class SendEmailsHandlersTests
{
    private readonly INotificationLogRepository _log = Substitute.For<INotificationLogRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    private NotificationDispatcher CreateDispatcher()
    {
        _log.ExistsAsync(Arg.Any<NotificationType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _log.TryAddAsync(Arg.Any<SentNotification>(), Arg.Any<CancellationToken>()).Returns(true);
        return new NotificationDispatcher(_log, _emailSender, NullLogger<NotificationDispatcher>.Instance);
    }

    [Fact]
    public async Task UserRegistered_DeberiaEnviarBienvenidaAlEmailDelUsuario()
    {
        var handler = new SendUserRegisteredEmailCommandHandler(CreateDispatcher());

        await handler.Handle(new SendUserRegisteredEmailCommand(Guid.NewGuid(), "ana@test.com", "Ana"), CancellationToken.None);

        await _emailSender.Received(1).SendAsync("ana@test.com", Arg.Is<string>(s => s.Contains("Bienvenido")), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderPaid_DeberiaEnviarConfirmacionConElTotal()
    {
        var handler = new SendOrderPaidEmailCommandHandler(CreateDispatcher());

        await handler.Handle(new SendOrderPaidEmailCommand(Guid.NewGuid(), "ana@test.com", "Ana", 30m, "USD"), CancellationToken.None);

        await _emailSender.Received(1).SendAsync("ana@test.com", Arg.Any<string>(), Arg.Is<string>(h => h.Contains("30.00 USD")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderShipped_DeberiaEnviarAvisoDeEnvio()
    {
        var handler = new SendOrderShippedEmailCommandHandler(CreateDispatcher());

        await handler.Handle(new SendOrderShippedEmailCommand(Guid.NewGuid(), "ana@test.com", "Ana"), CancellationToken.None);

        await _emailSender.Received(1).SendAsync("ana@test.com", Arg.Is<string>(s => s.Contains("va en camino")), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

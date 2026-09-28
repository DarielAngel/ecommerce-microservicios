using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Domain.Enums;
using MediatR;

namespace Ecommerce.Notifications.Application.Features;

// ---- Bienvenida (evento UserRegistered) ----
public record SendUserRegisteredEmailCommand(Guid UserId, string Email, string FullName) : IRequest;

public class SendUserRegisteredEmailCommandHandler : IRequestHandler<SendUserRegisteredEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendUserRegisteredEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendUserRegisteredEmailCommand request, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.Welcome(request.FullName);
        return _dispatcher.DispatchAsync(NotificationType.UserRegistered, request.UserId, request.Email, subject, html, ct);
    }
}

// ---- Confirmación de pago (evento OrderPaid) ----
public record SendOrderPaidEmailCommand(
    Guid OrderId, string Email, string FullName, decimal TotalAmount, string Currency) : IRequest;

public class SendOrderPaidEmailCommandHandler : IRequestHandler<SendOrderPaidEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendOrderPaidEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendOrderPaidEmailCommand request, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.OrderPaid(request.OrderId, request.FullName, request.TotalAmount, request.Currency);
        return _dispatcher.DispatchAsync(NotificationType.OrderPaid, request.OrderId, request.Email, subject, html, ct);
    }
}

// ---- Pedido enviado (evento OrderShipped) ----
public record SendOrderShippedEmailCommand(Guid OrderId, string Email, string FullName) : IRequest;

public class SendOrderShippedEmailCommandHandler : IRequestHandler<SendOrderShippedEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendOrderShippedEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendOrderShippedEmailCommand request, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.OrderShipped(request.OrderId, request.FullName);
        return _dispatcher.DispatchAsync(NotificationType.OrderShipped, request.OrderId, request.Email, subject, html, ct);
    }
}

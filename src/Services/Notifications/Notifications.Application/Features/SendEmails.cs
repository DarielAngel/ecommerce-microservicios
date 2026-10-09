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

// ---- Carrito abandonado (evento CartAbandoned, Fase 6) ----
public record SendCartAbandonedEmailCommand(
    Guid ReminderId, string Email, string FullName, IReadOnlyList<EmailTemplates.CartLine> Items, decimal Subtotal,
    string CartUrl) : IRequest;

public class SendCartAbandonedEmailCommandHandler : IRequestHandler<SendCartAbandonedEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendCartAbandonedEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendCartAbandonedEmailCommand request, CancellationToken ct)
    {
        if (request.Items.Count == 0) return Task.CompletedTask; // nada que recordar
        var (subject, html) = EmailTemplates.CartAbandoned(request.FullName, request.Items, request.Subtotal, request.CartUrl);
        return _dispatcher.DispatchAsync(NotificationType.CartAbandoned, request.ReminderId, request.Email, subject, html, ct);
    }
}

// ---- Devolución reembolsada / rechazada (Fase 7) ----
public record SendReturnRefundedEmailCommand(
    Guid ReturnId, Guid OrderId, string Email, string FullName, IReadOnlyList<EmailTemplates.RefundLine> Items,
    decimal Amount, string Currency, bool OrderFullyRefunded, int PointsRestored, bool OrderCancelled = false,
    string? CouponCode = null) : IRequest;

public class SendReturnRefundedEmailCommandHandler : IRequestHandler<SendReturnRefundedEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendReturnRefundedEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendReturnRefundedEmailCommand request, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.ReturnRefunded(
            request.OrderId, request.FullName, request.Items, request.Amount, request.Currency, request.OrderFullyRefunded,
            request.PointsRestored, request.OrderCancelled, request.CouponCode);
        return _dispatcher.DispatchAsync(NotificationType.ReturnRefunded, request.ReturnId, request.Email, subject, html, ct);
    }
}

public record SendReturnRejectedEmailCommand(Guid ReturnId, Guid OrderId, string Email, string FullName, string Note,
    bool IsCancellation = false) : IRequest;

public class SendReturnRejectedEmailCommandHandler : IRequestHandler<SendReturnRejectedEmailCommand>
{
    private readonly NotificationDispatcher _dispatcher;

    public SendReturnRejectedEmailCommandHandler(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task Handle(SendReturnRejectedEmailCommand request, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.ReturnRejected(request.OrderId, request.FullName, request.Note, request.IsCancellation);
        return _dispatcher.DispatchAsync(NotificationType.ReturnRejected, request.ReturnId, request.Email, subject, html, ct);
    }
}

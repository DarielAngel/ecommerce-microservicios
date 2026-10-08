using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

// =====================================================================================================
// Devoluciones y reembolsos (Fase 7)
//
//   cliente: pide la devolución (Requested)
//   Admin:   aprueba → el monto queda fijado (Approved) → Pagos reembolsa en PayPal (Refunded)
//            o rechaza con una nota (Rejected)
//
// Al quedar Refunded se publica OrderRefundedEvent: Inventario repone el stock, Lealtad ajusta los
// puntos y Notificaciones avisa. Si Pagos no responde, la devolución queda Approved y el Admin puede
// reintentar: el reembolso usa el id de la devolución como clave, así PayPal nunca paga dos veces.
// =====================================================================================================

public record ReturnLineResult(Guid VariantId, Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

/// <summary>Una devolución tal como la ve el cliente (dentro de su pedido).</summary>
public record ReturnResult(
    Guid ReturnId, Guid OrderId, string Status, string Reason, string? Comment, string? AdminNote,
    decimal RefundAmount, int LoyaltyPointsToRestore, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, DateTime? RefundedAtUtc,
    IReadOnlyList<ReturnLineResult> Lines)
{
    public static ReturnResult From(Order order, OrderReturn r) => new(
        r.Id, order.Id, r.Status.ToString(), r.Reason.ToString(), r.Comment, r.AdminNote,
        r.RefundAmount, r.LoyaltyPointsToRestore, r.CreatedAtUtc, r.ResolvedAtUtc, r.RefundedAtUtc,
        r.Lines.Select(l => new ReturnLineResult(l.VariantId, l.ProductId, l.ProductName, l.UnitPrice, l.Quantity)).ToList());
}

/// <summary>
/// Una devolución tal como la ve el Admin: con el cliente, el pedido y cuánto se devolvería (si todavía no se
/// aprobó, <see cref="RefundAmount"/> es lo que se va a devolver al aprobar).
/// </summary>
public record AdminReturnResult(
    Guid ReturnId, Guid OrderId, string Status, string Reason, string? Comment, string? AdminNote,
    decimal RefundAmount, int LoyaltyPointsToRestore, bool CompletesOrder,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, DateTime? RefundedAtUtc, IReadOnlyList<ReturnLineResult> Lines,
    string UserEmail, string UserFullName, decimal OrderTotal, decimal OrderRefundedAmount, DateTime? ShippedAtUtc)
{
    public static AdminReturnResult From(Order order, OrderReturn r)
    {
        var quote = r.Status == ReturnStatus.Rejected ? new RefundQuote(0, 0, false) : order.QuoteRefund(r.Id);
        var basic = ReturnResult.From(order, r);
        return new AdminReturnResult(
            r.Id, order.Id, basic.Status, basic.Reason, r.Comment, r.AdminNote,
            quote.Amount, quote.LoyaltyPointsToRestore, quote.CompletesOrder,
            r.CreatedAtUtc, r.ResolvedAtUtc, r.RefundedAtUtc, basic.Lines,
            order.UserEmail, order.UserFullName, order.TotalAmount, order.RefundedAmount, order.ShippedAtUtc);
    }
}

// ---- El cliente pide la devolución ----

public record ReturnItemInput(Guid VariantId, int Quantity);

public record RequestReturnCommand(Guid OrderId, Guid UserId, IReadOnlyList<ReturnItemInput> Items, string Reason, string? Comment)
    : IRequest<CheckoutResult>;

public class RequestReturnCommandValidator : AbstractValidator<RequestReturnCommand>
{
    public RequestReturnCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Elige al menos un producto para devolver.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Las cantidades deben ser mayores a cero.");
        });
        RuleFor(x => x.Reason)
            .Must(r => Enum.TryParse<ReturnReason>(r, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(r, out _))
            .WithMessage("Elige el motivo de la devolución.");
        RuleFor(x => x.Comment).MaximumLength(OrderReturn.MaxCommentLength)
            .WithMessage($"El comentario admite hasta {OrderReturn.MaxCommentLength} caracteres.");
    }
}

public class RequestReturnCommandHandler : IRequestHandler<RequestReturnCommand, CheckoutResult>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderLock _orderLock;
    private readonly TimeProvider _time;

    public RequestReturnCommandHandler(IOrderRepository orders, IOrderLock orderLock, TimeProvider time)
    {
        _orders = orders;
        _orderLock = orderLock;
        _time = time;
    }

    public async Task<CheckoutResult> Handle(RequestReturnCommand request, CancellationToken ct)
    {
        // Con el pedido bloqueado: dos solicitudes a la vez (doble clic) no pueden pasar ambas el chequeo de
        // "ya hay una devolución en curso".
        await using var orderLock = await _orderLock.AcquireAsync(request.OrderId, ct);

        var order = await _orders.GetByIdAsync(request.OrderId, ct);
        if (order is null || order.UserId != request.UserId)
            throw new NotFoundAppException("La orden no existe.");

        order.RequestReturn(
            request.Items.Select(i => (i.VariantId, i.Quantity)),
            Enum.Parse<ReturnReason>(request.Reason, ignoreCase: true),
            request.Comment,
            _time.GetUtcNow().UtcDateTime);
        await _orders.SaveChangesAsync(ct);

        return CheckoutResult.From(order);
    }
}

// ---- El Admin las ve ----

public record ListReturnsQuery(string? Status = null, int Count = 100) : IRequest<IReadOnlyList<AdminReturnResult>>;

public class ListReturnsQueryValidator : AbstractValidator<ListReturnsQuery>
{
    public ListReturnsQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<ReturnStatus>(s, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(s, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Estado inválido: usa Requested, Approved, Refunded o Rejected.");
        RuleFor(x => x.Count).InclusiveBetween(1, 500);
    }
}

public class ListReturnsQueryHandler : IRequestHandler<ListReturnsQuery, IReadOnlyList<AdminReturnResult>>
{
    private readonly IOrderRepository _orders;

    public ListReturnsQueryHandler(IOrderRepository orders) => _orders = orders;

    public async Task<IReadOnlyList<AdminReturnResult>> Handle(ListReturnsQuery request, CancellationToken ct)
    {
        ReturnStatus? status = string.IsNullOrWhiteSpace(request.Status)
            ? null
            : Enum.Parse<ReturnStatus>(request.Status, ignoreCase: true);
        var orders = await _orders.ListWithReturnsAsync(status, request.Count, ct);

        return orders
            .SelectMany(o => o.Returns.Where(r => status is null || r.Status == status).Select(r => AdminReturnResult.From(o, r)))
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(request.Count)
            .ToList();
    }
}

// ---- El Admin aprueba (y se reembolsa) ----

/// <param name="AccessToken">Token del Admin: Pagos solo acepta reembolsos de un Admin.</param>
public record ApproveReturnCommand(Guid ReturnId, string? Note, string AccessToken) : IRequest<AdminReturnResult>;

public class ApproveReturnCommandValidator : AbstractValidator<ApproveReturnCommand>
{
    public ApproveReturnCommandValidator()
    {
        RuleFor(x => x.Note).MaximumLength(OrderReturn.MaxCommentLength);
    }
}

public class ApproveReturnCommandHandler : IRequestHandler<ApproveReturnCommand, AdminReturnResult>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderLock _orderLock;
    private readonly IPaymentServiceClient _payments;
    private readonly IEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly ILogger<ApproveReturnCommandHandler> _logger;

    public ApproveReturnCommandHandler(
        IOrderRepository orders, IOrderLock orderLock, IPaymentServiceClient payments, IEventPublisher events,
        TimeProvider time, ILogger<ApproveReturnCommandHandler> logger)
    {
        _orders = orders;
        _orderLock = orderLock;
        _payments = payments;
        _events = events;
        _time = time;
        _logger = logger;
    }

    public async Task<AdminReturnResult> Handle(ApproveReturnCommand request, CancellationToken ct)
    {
        var orderId = await _orders.FindOrderIdByReturnIdAsync(request.ReturnId, ct)
            ?? throw new NotFoundAppException("La devolución no existe.");

        // 1) Aprobar y GUARDAR (commit) antes de mover dinero: el monto queda fijado. El bloqueo del pedido es una
        //    transacción, así que se suelta (y confirma) ANTES de llamar a Pagos: si el proceso se cae después de
        //    que PayPal reembolsó, la devolución ya figura aprobada y reintentar reembolsa exactamente lo mismo
        //    (Pagos usa el id de la devolución como clave), nunca vuelve a "pedida".
        OrderReturn orderReturn;
        await using (await _orderLock.AcquireAsync(orderId, ct))
        {
            var order = await _orders.GetByIdFreshAsync(orderId, ct) ?? throw new NotFoundAppException("La devolución no existe.");
            orderReturn = order.ApproveReturn(request.ReturnId, request.Note, _time.GetUtcNow().UtcDateTime);
            await _orders.SaveChangesAsync(ct);
        }

        // 2) Reembolsar fuera de la transacción (si ya estaba reembolsada, no se vuelve a cobrar nada).
        if (orderReturn.Status == ReturnStatus.Approved)
        {
            try
            {
                await _payments.RefundAsync(
                    orderId, orderReturn.Id, orderReturn.RefundAmount, $"Devolución del pedido #{ShortId(orderId)}",
                    request.AccessToken, ct);
            }
            catch (ConflictAppException ex)
            {
                _logger.LogWarning("Pagos rechazó el reembolso de la devolución {ReturnId}: {Reason}", orderReturn.Id, ex.Message);
                throw new ConflictAppException(
                    $"No se pudo reembolsar: {ex.Message} La devolución quedó aprobada; puedes reintentar o cancelarla.");
            }
        }

        // 3) Marcar reembolsada con datos frescos (otro Admin pudo terminarla mientras tanto) y confirmar.
        Order refreshed;
        await using (await _orderLock.AcquireAsync(orderId, ct))
        {
            refreshed = await _orders.GetByIdFreshAsync(orderId, ct) ?? throw new NotFoundAppException("La devolución no existe.");
            refreshed.MarkReturnRefunded(orderReturn.Id, _time.GetUtcNow().UtcDateTime);
            await _orders.SaveChangesAsync(ct);
        }

        // 4) Recién con todo confirmado se avisa a Inventario, Lealtad y Notificaciones. Si una aprobación anterior
        //    no pudo publicar, esta lo reenvía (los consumidores ignoran los repetidos).
        var refundedReturn = refreshed.Returns.Single(r => r.Id == orderReturn.Id);
        await PublishRefundedAsync(refreshed, refundedReturn, ct);
        return AdminReturnResult.From(refreshed, refundedReturn);
    }

    private async Task PublishRefundedAsync(Order order, OrderReturn orderReturn, CancellationToken ct)
    {
        try
        {
            await _events.PublishAsync(RefundEvents.For(order, orderReturn, _time), ct);
        }
        catch (Exception ex)
        {
            // El dinero ya se devolvió: no se deshace. El Admin puede volver a "aprobar" para reenviar el evento.
            _logger.LogError(ex, "No se pudo publicar OrderRefundedEvent de la devolución {ReturnId}; reintentar la aprobación lo reenvía.",
                orderReturn.Id);
        }
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}

// ---- El Admin rechaza ----

/// <summary>
/// Rechaza una devolución pedida. También sirve para cancelar una ya aprobada cuyo reembolso nunca se hizo (por
/// ejemplo, PayPal lo rechaza siempre): antes se le pregunta a Pagos, y si en realidad el reembolso SÍ existe, la
/// devolución se da por reembolsada en vez de rechazarla.
/// </summary>
/// <param name="AccessToken">Token del Admin, para consultar a Pagos.</param>
public record RejectReturnCommand(Guid ReturnId, string Note, string AccessToken = "") : IRequest<AdminReturnResult>;

public class RejectReturnCommandValidator : AbstractValidator<RejectReturnCommand>
{
    public RejectReturnCommandValidator()
    {
        RuleFor(x => x.Note).NotEmpty().WithMessage("Escribe una nota para el cliente explicando por qué se rechaza.")
            .MaximumLength(OrderReturn.MaxCommentLength);
    }
}

public class RejectReturnCommandHandler : IRequestHandler<RejectReturnCommand, AdminReturnResult>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderLock _orderLock;
    private readonly IPaymentServiceClient _payments;
    private readonly IEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly ILogger<RejectReturnCommandHandler> _logger;

    public RejectReturnCommandHandler(
        IOrderRepository orders, IOrderLock orderLock, IPaymentServiceClient payments, IEventPublisher events, TimeProvider time,
        ILogger<RejectReturnCommandHandler> logger)
    {
        _orders = orders;
        _orderLock = orderLock;
        _payments = payments;
        _events = events;
        _time = time;
        _logger = logger;
    }

    public async Task<AdminReturnResult> Handle(RejectReturnCommand request, CancellationToken ct)
    {
        var orderId = await _orders.FindOrderIdByReturnIdAsync(request.ReturnId, ct)
            ?? throw new NotFoundAppException("La devolución no existe.");

        // Aprobada: ¿el reembolso llegó a hacerse? (se pregunta fuera del bloqueo: es una llamada HTTP).
        var current = (await _orders.GetByIdFreshAsync(orderId, ct))?.Returns.FirstOrDefault(r => r.Id == request.ReturnId);
        var approvedWithoutRefund = false;
        if (current?.Status == ReturnStatus.Approved)
        {
            if (await _payments.FindRefundAsync(orderId, request.ReturnId, request.AccessToken, ct) is not null)
            {
                Order healed;
                await using (await _orderLock.AcquireAsync(orderId, ct))
                {
                    healed = await _orders.GetByIdFreshAsync(orderId, ct) ?? throw new NotFoundAppException("La devolución no existe.");
                    healed.MarkReturnRefunded(request.ReturnId, _time.GetUtcNow().UtcDateTime);
                    await _orders.SaveChangesAsync(ct);
                }
                await PublishRefundedAsync(healed, healed.Returns.Single(r => r.Id == request.ReturnId), ct);
                throw new ConflictAppException(
                    "Ese reembolso sí se hizo en PayPal, así que la devolución quedó como reembolsada (no se puede rechazar).");
            }
            approvedWithoutRefund = true;
        }

        Order order;
        OrderReturn orderReturn;
        bool alreadyRejected;
        await using (await _orderLock.AcquireAsync(orderId, ct))
        {
            order = await _orders.GetByIdFreshAsync(orderId, ct) ?? throw new NotFoundAppException("La devolución no existe.");
            alreadyRejected = order.Returns.Any(r => r.Id == request.ReturnId && r.Status == ReturnStatus.Rejected);
            orderReturn = order.RejectReturn(request.ReturnId, request.Note, _time.GetUtcNow().UtcDateTime, approvedWithoutRefund);
            await _orders.SaveChangesAsync(ct);
        }

        if (!alreadyRejected)
        {
            try
            {
                await _events.PublishAsync(new ReturnRejectedEvent(
                    orderReturn.Id, order.Id, order.UserId, order.UserEmail, order.UserFullName,
                    orderReturn.AdminNote!, orderReturn.ResolvedAtUtc!.Value), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo publicar ReturnRejectedEvent de {ReturnId} (no crítico).", orderReturn.Id);
            }
        }

        return AdminReturnResult.From(order, orderReturn);
    }

    private async Task PublishRefundedAsync(Order order, OrderReturn orderReturn, CancellationToken ct)
    {
        try
        {
            await _events.PublishAsync(RefundEvents.For(order, orderReturn, _time), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar OrderRefundedEvent de la devolución {ReturnId}.", orderReturn.Id);
        }
    }
}

internal static class RefundEvents
{
    public static OrderRefundedEvent For(Order order, OrderReturn orderReturn, TimeProvider time) => new(
        orderReturn.Id, order.Id, order.UserId, order.UserEmail, order.UserFullName,
        orderReturn.RefundAmount, "USD", order.Status == Domain.Enums.OrderStatus.Refunded,
        orderReturn.LoyaltyPointsToRestore,
        orderReturn.Lines.Select(l => new RefundedItem(l.VariantId, l.ProductId, l.ProductName, l.Quantity)).ToList(),
        orderReturn.RefundedAtUtc ?? time.GetUtcNow().UtcDateTime);
}

using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

/// <summary>
/// Cancelar un pedido antes de que se envíe (Fase 7):
/// <list type="bullet">
/// <item><b>Pago pendiente</b>: se cancela al instante y se liberan el stock, el cupón y los puntos apartados.</item>
/// <item><b>Pagado, pedido por el cliente</b>: queda una solicitud de cancelación (una devolución de todo el pedido)
/// que el Admin aprueba (se reembolsa todo) o rechaza. Solo un Admin puede mover dinero, y mientras está pendiente
/// el pedido no se puede enviar.</item>
/// <item><b>Pagado, cancelado por un Admin</b>: se crea (o se toma la que pidió el cliente) y se aprueba en el acto.</item>
/// </list>
/// </summary>
/// <param name="AccessToken">Token de quien cancela: para liberar reservas (cliente) o reembolsar (Admin).</param>
public record CancelOrderCommand(
    Guid OrderId, Guid RequesterId, bool RequesterIsAdmin, string Reason, string? Comment, string AccessToken)
    : IRequest<CheckoutResult>;

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => Enum.TryParse<ReturnReason>(r, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(r, out _))
            .WithMessage("Elige el motivo de la cancelación.");
        RuleFor(x => x.Comment).MaximumLength(OrderReturn.MaxCommentLength);
    }
}

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CheckoutResult>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderLock _orderLock;
    private readonly IInventoryServiceClient _inventory;
    private readonly ICouponServiceClient _coupons;
    private readonly ILoyaltyServiceClient _loyalty;
    private readonly IPaymentServiceClient _payments;
    private readonly ISender _mediator;
    private readonly TimeProvider _time;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderRepository orders, IOrderLock orderLock, IInventoryServiceClient inventory, ICouponServiceClient coupons,
        ILoyaltyServiceClient loyalty, IPaymentServiceClient payments, ISender mediator, TimeProvider time,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _payments = payments;
        _orders = orders;
        _orderLock = orderLock;
        _inventory = inventory;
        _coupons = coupons;
        _loyalty = loyalty;
        _mediator = mediator;
        _time = time;
        _logger = logger;
    }

    public async Task<CheckoutResult> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        Guid? approveNow = null;
        Order order;

        // Bloqueado: cancelar y confirmar el pago a la vez (dos pestañas) se atienden de a uno.
        await using (await _orderLock.AcquireAsync(request.OrderId, ct))
        {
            order = await _orders.GetByIdFreshAsync(request.OrderId, ct) ?? throw new NotFoundAppException("La orden no existe.");
            if (!request.RequesterIsAdmin && order.UserId != request.RequesterId)
                throw new NotFoundAppException("La orden no existe.");

            var now = _time.GetUtcNow().UtcDateTime;
            if (order.Status == OrderStatus.PendingPayment)
            {
                // Las reservas de cupón y puntos son del cliente: solo él puede soltarlas (y no hay dinero que mover).
                if (order.UserId != request.RequesterId)
                    throw new ConflictAppException("Un pedido sin pagar solo lo puede cancelar el cliente.");

                // ¿PayPal ya cobró aunque el pedido siga "pendiente"? (la confirmación se cortó a mitad de camino).
                // Entonces no se cancela así: se confirmaría el pago y luego se pediría la cancelación con reembolso.
                if (await _payments.GetPaymentStatusAsync(order.Id, request.AccessToken, ct) is "Captured" or "Refunded")
                    throw new ConflictAppException(
                        "Tu pago ya se cobró. Toca \"Ya aprobé el pago — confirmar\" para terminar la compra; después puedes pedir la cancelación y te devolvemos todo.");

                // Primero se libera el stock (si falla, no se cancela nada y se puede reintentar); cupón y puntos
                // son de mejor esfuerzo, como cuando falla un pago (además vencen solos).
                await _inventory.ReleaseReservationAsync(order.Id, request.AccessToken, ct);
                await BestEffortAsync(order.CouponCode is not null, () => _coupons.ReleaseAsync(order.Id, request.AccessToken, ct), "cupón", order.Id);
                await BestEffortAsync(order.LoyaltyPoints > 0, () => _loyalty.ReleaseAsync(order.Id, request.AccessToken, ct), "puntos", order.Id);
                order.MarkCancelled(now);
                await _orders.SaveChangesAsync(ct);
                return CheckoutResult.From(order);
            }

            var reason = Enum.Parse<ReturnReason>(request.Reason, ignoreCase: true);
            if (request.RequesterIsAdmin && order.OpenReturn is { IsCancellation: true } pending)
            {
                approveNow = pending.Id; // el cliente ya la había pedido: el Admin la aprueba
            }
            else
            {
                // El comentario del Admin va como nota de la aprobación, no como si lo hubiera escrito el cliente.
                var cancellation = order.RequestCancellation(reason, request.RequesterIsAdmin ? null : request.Comment, now);
                await _orders.SaveChangesAsync(ct);
                if (request.RequesterIsAdmin) approveNow = cancellation.Id;
            }
        }

        if (approveNow is { } returnId)
        {
            // Mismo camino que aprobar una devolución: monto fijado y guardado, reembolso, evento.
            await _mediator.Send(new ApproveReturnCommand(returnId, request.Comment, request.AccessToken), ct);
            order = await _orders.GetByIdFreshAsync(request.OrderId, ct) ?? order;
        }

        return CheckoutResult.From(order);
    }

    private async Task BestEffortAsync(bool applies, Func<Task> release, string what, Guid orderId)
    {
        if (!applies) return;
        try
        {
            await release();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo liberar el {What} de la orden cancelada {OrderId}.", what, orderId);
        }
    }
}

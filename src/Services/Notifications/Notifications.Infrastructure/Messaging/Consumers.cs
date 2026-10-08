using Ecommerce.Contracts.Events;
using Ecommerce.Notifications.Application.Features;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.Notifications.Infrastructure.Messaging;

/// <summary>
/// Cada consumidor traduce un evento externo (Contracts) al comando interno de Application.
/// Si el envío falla, la excepción sube y MassTransit reintenta (ver configuración del bus).
/// </summary>
public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly ISender _mediator;

    public UserRegisteredConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<UserRegisteredEvent> context) =>
        _mediator.Send(
            new SendUserRegisteredEmailCommand(context.Message.UserId, context.Message.Email, context.Message.FullName),
            context.CancellationToken);
}

public class OrderPaidConsumer : IConsumer<OrderPaidEvent>
{
    private readonly ISender _mediator;

    public OrderPaidConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<OrderPaidEvent> context) =>
        _mediator.Send(
            new SendOrderPaidEmailCommand(
                context.Message.OrderId, context.Message.Email, context.Message.FullName,
                context.Message.TotalAmount, context.Message.Currency),
            context.CancellationToken);
}

public class OrderShippedConsumer : IConsumer<OrderShippedEvent>
{
    private readonly ISender _mediator;

    public OrderShippedConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<OrderShippedEvent> context) =>
        _mediator.Send(
            new SendOrderShippedEmailCommand(context.Message.OrderId, context.Message.Email, context.Message.FullName),
            context.CancellationToken);
}

public class CartAbandonedConsumer : IConsumer<CartAbandonedEvent>
{
    private readonly ISender _mediator;
    private readonly string _storeUrl;

    public CartAbandonedConsumer(ISender mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        // Dónde está la tienda, para el botón "Volver a mi carrito" (en producción, el dominio real).
        _storeUrl = (configuration["Store:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public Task Consume(ConsumeContext<CartAbandonedEvent> context)
    {
        var m = context.Message;
        return _mediator.Send(
            new SendCartAbandonedEmailCommand(
                m.ReminderId, m.Email, m.FullName,
                m.Items.Select(i => new Application.Common.EmailTemplates.CartLine(i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
                m.Subtotal, $"{_storeUrl}/cart"),
            context.CancellationToken);
    }
}

/// <summary>
/// Devolución reembolsada (Fase 7). La cola se llama "OrderRefunded"; Inventario y Lealtad escuchan el mismo
/// evento con colas propias ("ReturnRestock" y "loyalty-order-refunded"), así cada servicio recibe su copia.
/// </summary>
public class OrderRefundedConsumer : IConsumer<OrderRefundedEvent>
{
    private readonly ISender _mediator;

    public OrderRefundedConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<OrderRefundedEvent> context)
    {
        var m = context.Message;
        return _mediator.Send(
            new SendReturnRefundedEmailCommand(
                m.ReturnId, m.OrderId, m.Email, m.FullName,
                m.Items.Select(i => new Application.Common.EmailTemplates.RefundLine(i.ProductName, i.Quantity)).ToList(),
                m.RefundAmount, m.Currency, m.OrderFullyRefunded, m.LoyaltyPointsToRestore, m.OrderCancelled),
            context.CancellationToken);
    }
}

public class ReturnRejectedConsumer : IConsumer<ReturnRejectedEvent>
{
    private readonly ISender _mediator;

    public ReturnRejectedConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<ReturnRejectedEvent> context) =>
        _mediator.Send(
            new SendReturnRejectedEmailCommand(context.Message.ReturnId, context.Message.OrderId, context.Message.Email,
                context.Message.FullName, context.Message.Note, context.Message.IsCancellation),
            context.CancellationToken);
}

using Ecommerce.Contracts.Events;
using Ecommerce.Loyalty.Application.Features;
using MassTransit;
using MediatR;

namespace Ecommerce.Loyalty.Infrastructure.Messaging;

/// <summary>
/// Cada compra pagada: (1) deja firme el canje de puntos de esa orden, si usó puntos — es la vía durable:
/// si falla, MassTransit reintenta; (2) suma los puntos por lo que efectivamente se cobró.
/// </summary>
public class OrderPaidConsumer : IConsumer<OrderPaidEvent>
{
    private readonly ISender _mediator;

    public OrderPaidConsumer(ISender mediator) => _mediator = mediator;

    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var m = context.Message;
        await _mediator.Send(new ConfirmPointsCommand(m.OrderId, RequesterId: null, OwnerId: m.UserId), context.CancellationToken);
        await _mediator.Send(new EarnPointsCommand(m.UserId, m.OrderId, m.TotalAmount), context.CancellationToken);
    }
}

using Ecommerce.Contracts.Events;
using Ecommerce.Reviews.Application.Features;
using MassTransit;
using MediatR;

namespace Ecommerce.Reviews.Infrastructure.Messaging;

/// <summary>Traduce el evento externo OrderPaid al comando interno que registra las compras verificadas.</summary>
public class OrderPaidConsumer : IConsumer<OrderPaidEvent>
{
    private readonly ISender _mediator;

    public OrderPaidConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<OrderPaidEvent> context) =>
        _mediator.Send(
            new RecordVerifiedPurchaseCommand(context.Message.UserId, context.Message.ProductIds ?? Array.Empty<Guid>()),
            context.CancellationToken);
}

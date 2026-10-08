using Ecommerce.Contracts.Events;
using Ecommerce.Inventory.Application.Features;
using MassTransit;
using MediatR;

namespace Ecommerce.Inventory.Infrastructure.Messaging;

/// <summary>
/// Repone el stock cuando una devolución se reembolsa (evento OrderRefunded de Órdenes, Fase 7).
/// OJO con el nombre de la clase: MassTransit nombra la cola a partir de él ("ReturnRestock"). Notificaciones
/// también escucha OrderRefunded con su propia cola ("OrderRefunded"); si las dos clases se llamaran igual,
/// compartirían la cola y cada mensaje le llegaría a uno solo de los dos servicios.
/// </summary>
public class ReturnRestockConsumer : IConsumer<OrderRefundedEvent>
{
    private readonly ISender _mediator;

    public ReturnRestockConsumer(ISender mediator) => _mediator = mediator;

    public Task Consume(ConsumeContext<OrderRefundedEvent> context) =>
        _mediator.Send(
            new RestockReturnedItemsCommand(
                context.Message.ReturnId,
                context.Message.Items.Select(i => (i.VariantId, i.Quantity)).ToList()),
            context.CancellationToken);
}

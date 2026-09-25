using Ecommerce.Contracts.Events;
using Ecommerce.Inventory.Application.Features;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Infrastructure.Messaging;

/// <summary>
/// Traduce el evento externo (Contracts.VariantCreatedEvent) al comando interno de Application.
/// MassTransit ya garantiza reintentos ante fallos transitorios (ver configuración del bus);
/// el handler en sí es idempotente (CreateStockItemCommandHandler no duplica si ya existe),
/// así que un reintento o una entrega duplicada del broker no corrompen el estado.
/// </summary>
public class VariantCreatedConsumer : IConsumer<VariantCreatedEvent>
{
    private readonly ISender _mediator;
    private readonly ILogger<VariantCreatedConsumer> _logger;

    public VariantCreatedConsumer(ISender mediator, ILogger<VariantCreatedConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VariantCreatedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Evento VariantCreated recibido: VariantId={VariantId}, Sku={Sku}", message.VariantId, message.Sku);

        await _mediator.Send(new CreateStockItemCommand(message.VariantId), context.CancellationToken);
    }
}

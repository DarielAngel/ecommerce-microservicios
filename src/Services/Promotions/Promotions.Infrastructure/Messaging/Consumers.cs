using Ecommerce.Contracts.Events;
using Ecommerce.Promotions.Application.Features;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Promotions.Infrastructure.Messaging;

/// <summary>
/// Pedido reembolsado (Fase 7). Si se devolvió COMPLETO (cancelado antes del envío, o devuelto entero), el uso del
/// cupón vuelve a estar disponible. Con una devolución parcial el cupón se usó en lo que el cliente se quedó, así que
/// no se toca. Si falla, MassTransit reintenta; repetirlo es seguro.
/// </summary>
public class OrderRefundedConsumer : IConsumer<OrderRefundedEvent>
{
    private readonly ISender _mediator;
    private readonly ILogger<OrderRefundedConsumer> _logger;

    public OrderRefundedConsumer(ISender mediator, ILogger<OrderRefundedConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderRefundedEvent> context)
    {
        var m = context.Message;
        if (!m.OrderFullyRefunded) return;

        var restored = await _mediator.Send(new RestoreRedemptionCommand(m.OrderId, m.UserId), context.CancellationToken);
        if (restored)
            _logger.LogInformation("Se devolvió el uso del cupón {Code} de la orden {OrderId} (reembolsada completa).",
                m.CouponCode, m.OrderId);
    }
}

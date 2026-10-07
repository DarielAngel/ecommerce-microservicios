using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

public record MarkOrderAsShippedCommand(Guid OrderId) : IRequest<CheckoutResult>;

public class MarkOrderAsShippedCommandHandler : IRequestHandler<MarkOrderAsShippedCommand, CheckoutResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<MarkOrderAsShippedCommandHandler> _logger;

    public MarkOrderAsShippedCommandHandler(
        IOrderRepository orderRepository, IEventPublisher eventPublisher, ILogger<MarkOrderAsShippedCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CheckoutResult> Handle(MarkOrderAsShippedCommand request, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("La orden no existe.");

        // Idempotente: si ya estaba marcada como enviada, no volvemos a publicar el evento
        // (evita mandar el email de "tu pedido va en camino" dos veces por un doble-click).
        if (order.Status != Domain.Enums.OrderStatus.Shipped)
        {
            order.MarkShipped();
            await _orderRepository.SaveChangesAsync(ct);

            try
            {
                await _eventPublisher.PublishAsync(
                    new OrderShippedEvent(order.Id, order.UserId, order.UserEmail, order.UserFullName, DateTime.UtcNow), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo publicar OrderShippedEvent para la orden {OrderId} (no crítico).", order.Id);
            }
        }

        return CheckoutResult.From(order);
    }
}

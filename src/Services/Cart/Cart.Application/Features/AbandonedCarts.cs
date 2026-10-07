using Ecommerce.Cart.Application.Common;
using Ecommerce.Contracts.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Cart.Application.Features;

/// <summary>
/// Busca carritos abandonados y avisa a Notificaciones para que mande UN correo de recordatorio por cada
/// uno (Fase 6, T6.2). Lo corre un proceso en segundo plano cada pocos minutos.
/// </summary>
/// <param name="IdleFor">Cuánto tiempo sin cambios hace falta para considerarlo abandonado (ej. 1 hora).</param>
/// <param name="MaxAge">Más viejo que esto no se recuerda (el estudio citado en el roadmap: dentro de las 24 h).</param>
public record DetectAbandonedCartsCommand(DateTime NowUtc, TimeSpan IdleFor, TimeSpan MaxAge, int BatchSize = 100) : IRequest<int>;

public class DetectAbandonedCartsCommandHandler : IRequestHandler<DetectAbandonedCartsCommand, int>
{
    private readonly ICartRepository _carts;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<DetectAbandonedCartsCommandHandler> _logger;

    public DetectAbandonedCartsCommandHandler(
        ICartRepository carts, IEventPublisher publisher, ILogger<DetectAbandonedCartsCommandHandler> logger)
    {
        _carts = carts;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> Handle(DetectAbandonedCartsCommand request, CancellationToken ct)
    {
        var candidates = await _carts.ListIdleAsync(
            request.NowUtc - request.MaxAge, request.NowUtc - request.IdleFor, request.BatchSize, ct);

        var sent = 0;
        foreach (var cart in candidates.Where(c => c.NeedsAbandonedReminder(request.NowUtc, request.IdleFor, request.MaxAge)))
        {
            // Primero se marca y se guarda, después se publica: si dos instancias corren a la vez o el
            // proceso se reinicia, preferimos perder un recordatorio a mandarlo dos veces.
            var previous = cart.AbandonedReminderForActivityAtUtc;
            cart.MarkAbandonedReminderSent();
            await _carts.SaveChangesAsync(ct);

            var message = new CartAbandonedEvent(
                Guid.NewGuid(), cart.Id, cart.UserId, cart.ContactEmail!, cart.ContactName ?? "Cliente",
                cart.Items.Select(i => new AbandonedCartItem(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
                cart.Subtotal, cart.UpdatedAtUtc, request.NowUtc);

            try
            {
                await _publisher.PublishAsync(message, ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo publicar el recordatorio del carrito {CartId}; se reintenta en la próxima vuelta.", cart.Id);
                cart.UndoAbandonedReminder(previous);
                await _carts.SaveChangesAsync(CancellationToken.None);
            }
        }

        if (sent > 0) _logger.LogInformation("Recordatorios de carrito abandonado enviados: {Count}.", sent);
        return sent;
    }
}

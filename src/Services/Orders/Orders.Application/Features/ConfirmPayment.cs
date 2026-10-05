using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

public record ConfirmPaymentCommand(Guid OrderId, string AccessToken) : IRequest<CheckoutResult>;

/// <summary>
/// Segundo tramo de la saga, disparado después de que el comprador aprueba el pago en PayPal
/// (paso manual, fuera de nuestro control). Este handler SIEMPRE deja la orden en un estado
/// terminal y consistente con el stock:
///   - Captura exitosa  → Orden Paid, stock confirmado (descontado definitivo), evento OrderPaid publicado.
///   - Captura fallida  → Orden Failed, stock liberado (vuelve a estar disponible para otros).
/// </summary>
public class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, CheckoutResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryServiceClient _inventoryClient;
    private readonly IPaymentServiceClient _paymentClient;
    private readonly ICartServiceClient _cartClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ConfirmPaymentCommandHandler> _logger;

    public ConfirmPaymentCommandHandler(
        IOrderRepository orderRepository,
        IInventoryServiceClient inventoryClient,
        IPaymentServiceClient paymentClient,
        ICartServiceClient cartClient,
        IEventPublisher eventPublisher,
        ILogger<ConfirmPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _inventoryClient = inventoryClient;
        _paymentClient = paymentClient;
        _cartClient = cartClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CheckoutResult> Handle(ConfirmPaymentCommand request, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("La orden no existe.");

        // Idempotente: si ya está en un estado terminal, no repetimos la captura ni tocamos
        // el stock otra vez (protege contra doble-click o un reintento del cliente).
        if (order.Status is Domain.Enums.OrderStatus.Paid or Domain.Enums.OrderStatus.Failed)
        {
            return MapToResult(order);
        }

        var captureResult = await _paymentClient.CapturePaymentAsync(order.Id, request.AccessToken, ct);

        if (!captureResult.Success)
        {
            _logger.LogInformation("Pago fallido para la orden {OrderId} — liberando stock reservado.", order.Id);
            order.MarkFailed($"El pago no se pudo capturar (estado de PayPal: {captureResult.Status}).");
            await _inventoryClient.ReleaseReservationAsync(order.Id, request.AccessToken, ct);
            await _orderRepository.SaveChangesAsync(ct);
            return MapToResult(order);
        }

        order.MarkPaid();
        await _inventoryClient.ConfirmReservationAsync(order.Id, request.AccessToken, ct);
        await _orderRepository.SaveChangesAsync(ct);

        // Publicamos DESPUÉS de guardar (si publicar falla, la orden ya quedó pagada de verdad
        // — no queremos revertir un pago real capturado solo porque RabbitMQ tuvo un hipo).
        try
        {
            await _eventPublisher.PublishAsync(
                new OrderPaidEvent(
                    order.Id, order.UserId, order.UserEmail, order.UserFullName, order.TotalAmount, "USD", DateTime.UtcNow,
                    order.Lines.Select(l => l.ProductId).Distinct().ToList()), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo publicar OrderPaidEvent para la orden {OrderId} (no crítico).", order.Id);
        }

        // Quitar del carrito los ítems que ya se compraron. Esto es una "mejor esfuerzo": si
        // Carrito no responde, no queremos revertir el pago ya capturado por eso — se loguea
        // y sigue, el peor caso es que el usuario vea un ítem repetido en su carrito.
        foreach (var line in order.Lines)
        {
            try
            {
                await _cartClient.RemoveItemAsync(line.VariantId, request.AccessToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudo quitar la variante {VariantId} del carrito tras pagar la orden {OrderId} (no crítico).",
                    line.VariantId, order.Id);
            }
        }

        return MapToResult(order);
    }

    private static CheckoutResult MapToResult(Domain.Entities.Order order) => new(
        order.Id, order.Status.ToString(), order.TotalAmount,
        order.Lines.Select(l => new OrderLineResult(l.VariantId, l.ProductName, l.Sku, l.UnitPrice, l.Quantity, l.LineTotal)).ToList(),
        ApproveUrl: null);
}

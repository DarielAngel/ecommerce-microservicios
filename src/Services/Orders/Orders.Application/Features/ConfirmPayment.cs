using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

/// <param name="RequesterId">Quien confirma: solo el dueño de la orden puede hacerlo (null = sin chequeo, solo pruebas).</param>
public record ConfirmPaymentCommand(Guid OrderId, string AccessToken, Guid? RequesterId = null) : IRequest<CheckoutResult>;

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
    private readonly ICouponServiceClient _couponClient;
    private readonly ILoyaltyServiceClient _loyaltyClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IOrderLock _orderLock;
    private readonly ILogger<ConfirmPaymentCommandHandler> _logger;

    public ConfirmPaymentCommandHandler(
        IOrderRepository orderRepository,
        IInventoryServiceClient inventoryClient,
        IPaymentServiceClient paymentClient,
        ICartServiceClient cartClient,
        ICouponServiceClient couponClient,
        ILoyaltyServiceClient loyaltyClient,
        IEventPublisher eventPublisher,
        IOrderLock orderLock,
        ILogger<ConfirmPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _inventoryClient = inventoryClient;
        _paymentClient = paymentClient;
        _cartClient = cartClient;
        _couponClient = couponClient;
        _loyaltyClient = loyaltyClient;
        _eventPublisher = eventPublisher;
        _orderLock = orderLock;
        _logger = logger;
    }

    public async Task<CheckoutResult> Handle(ConfirmPaymentCommand request, CancellationToken ct)
    {
        // Una confirmación a la vez por orden (ver IOrderLock): sin esto, un doble clic podía capturar en
        // una petición y, en la otra, tomar el camino de "pago fallido" y devolver stock, cupón y puntos.
        await using var orderLock = await _orderLock.AcquireAsync(request.OrderId, ct);

        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("La orden no existe.");

        // La orden de otro cliente "no existe" para quien llama.
        if (request.RequesterId is { } requester && order.UserId != requester)
            throw new NotFoundAppException("La orden no existe.");

        // Idempotente: si ya está en un estado terminal, no repetimos la captura ni tocamos
        // el stock otra vez (protege contra doble-click o un reintento del cliente).
        if (order.Status is not Domain.Enums.OrderStatus.PendingPayment)
        {
            return MapToResult(order);
        }

        // Si la orden usa puntos, se vuelven a apartar ANTES de cobrar: si el checkout esperó tanto que la
        // reserva venció y esos puntos se usaron en otra compra, no se cobra esta con un descuento que ya
        // no existe (Lealtad responde 409 y la orden queda fallida, sin capturar el pago).
        if (order.LoyaltyPoints > 0)
        {
            try
            {
                await _loyaltyClient.ReserveAsync(order.Id, order.Subtotal - order.DiscountAmount, request.AccessToken, ct);
            }
            catch (ConflictAppException ex)
            {
                _logger.LogInformation("La orden {OrderId} no se cobra: {Reason}", order.Id, ex.Message);
                order.MarkFailed(ex.Message);
                await _inventoryClient.ReleaseReservationAsync(order.Id, request.AccessToken, ct);
                await ReleaseCouponBestEffortAsync(order, request.AccessToken, ct);
                await ReleasePointsBestEffortAsync(order, request.AccessToken, ct);
                await _orderRepository.SaveChangesAsync(ct);
                return MapToResult(order);
            }
        }

        var captureResult = await _paymentClient.CapturePaymentAsync(order.Id, request.AccessToken, ct);

        if (!captureResult.Success)
        {
            _logger.LogInformation("Pago fallido para la orden {OrderId} — liberando stock reservado.", order.Id);
            order.MarkFailed($"El pago no se pudo capturar (estado de PayPal: {captureResult.Status}).");
            await _inventoryClient.ReleaseReservationAsync(order.Id, request.AccessToken, ct);
            await ReleaseCouponBestEffortAsync(order, request.AccessToken, ct);
            await ReleasePointsBestEffortAsync(order, request.AccessToken, ct);
            await _orderRepository.SaveChangesAsync(ct);
            return MapToResult(order);
        }

        order.MarkPaid();
        await _inventoryClient.ConfirmReservationAsync(order.Id, request.AccessToken, ct);
        await _orderRepository.SaveChangesAsync(ct);

        // El pago ya se capturó: confirmar el uso del cupón no puede revertirlo. Si Promociones no
        // responde se loguea; la reserva sigue contando para el límite durante 2 horas.
        if (order.CouponCode is not null)
        {
            try
            {
                await _couponClient.ConfirmAsync(order.Id, request.AccessToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo confirmar el cupón {Coupon} de la orden {OrderId} (no crítico).",
                    order.CouponCode, order.Id);
            }
        }

        // Lo mismo con los puntos. Si Lealtad no responde, no se pierde: al recibir OrderPaid (más abajo,
        // con reintentos de RabbitMQ) Lealtad deja firme el canje por su cuenta.
        if (order.LoyaltyPoints > 0)
        {
            try
            {
                await _loyaltyClient.ConfirmAsync(order.Id, request.AccessToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo confirmar el canje de puntos de la orden {OrderId} (no crítico).", order.Id);
            }
        }

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

    private async Task ReleaseCouponBestEffortAsync(Domain.Entities.Order order, string accessToken, CancellationToken ct)
    {
        if (order.CouponCode is null) return;
        try
        {
            await _couponClient.ReleaseAsync(order.Id, accessToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo liberar el cupón {Coupon} de la orden fallida {OrderId}.", order.CouponCode, order.Id);
        }
    }

    private async Task ReleasePointsBestEffortAsync(Domain.Entities.Order order, string accessToken, CancellationToken ct)
    {
        if (order.LoyaltyPoints == 0) return;
        try
        {
            await _loyaltyClient.ReleaseAsync(order.Id, accessToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron devolver los puntos de la orden fallida {OrderId}.", order.Id);
        }
    }

    private static CheckoutResult MapToResult(Domain.Entities.Order order) => CheckoutResult.From(order);
}

using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

public record CheckoutCommand(
    Guid UserId, List<Guid> VariantIdsToCheckout, string ShippingAddress, string AccessToken) : IRequest<CheckoutResult>;

public record OrderLineResult(Guid VariantId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public record CheckoutResult(
    Guid OrderId, string Status, decimal TotalAmount, IReadOnlyList<OrderLineResult> Lines, string? ApproveUrl);

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.VariantIdsToCheckout).NotEmpty()
            .WithMessage("Selecciona al menos un ítem del carrito para hacer checkout.");
        RuleFor(x => x.ShippingAddress).NotEmpty()
            .WithMessage("La dirección de envío es obligatoria.");
    }
}

/// <summary>
/// Orquesta el primer tramo de la saga: Carrito → Inventario → Pagos. Si CUALQUIER paso falla,
/// se deshacen los pasos anteriores (compensación) para no dejar stock reservado húerfano ni
/// una orden a medias. El segundo tramo (capturar el pago y confirmar/liberar) vive en
/// ConfirmPaymentCommandHandler, porque requiere que el comprador apruebe en PayPal primero
/// — un paso manual que no se puede orquestar de punta a punta en una sola llamada HTTP.
/// </summary>
public class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, CheckoutResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICartServiceClient _cartClient;
    private readonly IInventoryServiceClient _inventoryClient;
    private readonly IPaymentServiceClient _paymentClient;
    private readonly ILogger<CheckoutCommandHandler> _logger;

    public CheckoutCommandHandler(
        IOrderRepository orderRepository,
        ICartServiceClient cartClient,
        IInventoryServiceClient inventoryClient,
        IPaymentServiceClient paymentClient,
        ILogger<CheckoutCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _cartClient = cartClient;
        _inventoryClient = inventoryClient;
        _paymentClient = paymentClient;
        _logger = logger;
    }

    public async Task<CheckoutResult> Handle(CheckoutCommand request, CancellationToken ct)
    {
        // Paso 1: traer el carrito real y quedarnos solo con los ítems seleccionados
        // (checkout parcial). Los precios y nombres vienen tal cual están en el carrito
        // (ya "congelados" desde que se agregaron ahí).
        var cartItems = await _cartClient.GetCartItemsAsync(request.AccessToken, ct);
        var selectedItems = cartItems.Where(i => request.VariantIdsToCheckout.Contains(i.VariantId)).ToList();

        if (selectedItems.Count == 0)
        {
            throw new ConflictAppException(
                "Ninguno de los ítems seleccionados está en tu carrito (puede que ya no exista o se haya quitado).");
        }

        if (selectedItems.Count != request.VariantIdsToCheckout.Distinct().Count())
        {
            throw new ConflictAppException("Alguno de los ítems seleccionados no está en tu carrito.");
        }

        var orderId = Guid.NewGuid();

        // Paso 2: reservar stock — todo o nada. Si falla, ni siquiera creamos la orden.
        var reservationLines = selectedItems.Select(i => new ReservationLineInput(i.VariantId, i.Quantity)).ToList();
        var reserved = await _inventoryClient.ReserveStockAsync(orderId, reservationLines, request.AccessToken, ct);

        if (!reserved)
        {
            _logger.LogInformation("Checkout {OrderId} falló: stock insuficiente.", orderId);
            throw new ConflictAppException("No hay stock suficiente para completar el checkout. Revisa las cantidades.");
        }

        // Paso 3: crear la orden ya con el stock asegurado, usando el MISMO orderId con el que
        // se reservó el stock en el paso 2 (ver el comentario en Order.Create para el porqué).
        var order = Order.Create(
            orderId,
            request.UserId,
            request.ShippingAddress,
            selectedItems.Select(i => (i.VariantId, i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity)));

        try
        {
            // Paso 4: crear el pago en PayPal por el total de la orden.
            var paymentResult = await _paymentClient.CreatePaymentAsync(
                order.Id, order.TotalAmount, "USD", request.AccessToken, ct);

            await _orderRepository.AddAsync(order, ct);
            await _orderRepository.SaveChangesAsync(ct);

            return new CheckoutResult(
                order.Id, order.Status.ToString(), order.TotalAmount,
                order.Lines.Select(MapLine).ToList(), paymentResult.ApproveUrl);
        }
        catch (Exception)
        {
            // Compensación: si crear el pago falla después de haber reservado stock, hay que
            // liberar esa reserva — si no, el stock queda "atrapado" para siempre.
            _logger.LogWarning(
                "Falló la creación del pago para {OrderId} después de reservar stock — liberando la reserva.", orderId);
            await _inventoryClient.ReleaseReservationAsync(orderId, request.AccessToken, ct);
            throw;
        }
    }

    private static OrderLineResult MapLine(Domain.Entities.OrderLine line) => new(
        line.VariantId, line.ProductName, line.Sku, line.UnitPrice, line.Quantity, line.LineTotal);
}

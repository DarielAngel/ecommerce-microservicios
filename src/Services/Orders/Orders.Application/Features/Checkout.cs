using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Application.Features;

public record CheckoutCommand(
    Guid UserId, string UserEmail, string UserFullName,
    List<Guid> VariantIdsToCheckout, string ShippingAddress, string AccessToken,
    string? CouponCode = null) : IRequest<CheckoutResult>;

public record OrderLineResult(Guid VariantId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal,
    Guid ProductId = default)
{
    public static OrderLineResult From(OrderLine l) =>
        new(l.VariantId, l.ProductName, l.Sku, l.UnitPrice, l.Quantity, l.LineTotal, l.ProductId);
}

/// <summary>
/// La orden tal como la ve el cliente. TotalAmount es lo que se cobra (ya con el descuento);
/// Subtotal y DiscountAmount permiten mostrar el desglose. Las fechas y la entrega estimada
/// (Fase 5) arman la línea de tiempo de "Mis pedidos".
/// </summary>
public record CheckoutResult(
    Guid OrderId, string Status, decimal TotalAmount, IReadOnlyList<OrderLineResult> Lines, string? ApproveUrl,
    decimal Subtotal, decimal DiscountAmount, string? CouponCode,
    string ShippingAddress = "", DateTime CreatedAtUtc = default, DateTime? PaidAtUtc = null, DateTime? ShippedAtUtc = null,
    DateOnly? EstimatedDeliveryFrom = null, DateOnly? EstimatedDeliveryTo = null)
{
    public static CheckoutResult From(Order order, string? approveUrl = null) => new(
        order.Id, order.Status.ToString(), order.TotalAmount,
        order.Lines.Select(OrderLineResult.From).ToList(),
        approveUrl, order.Subtotal, order.DiscountAmount, order.CouponCode,
        order.ShippingAddress, order.CreatedAtUtc, order.PaidAtUtc, order.ShippedAtUtc,
        order.EstimatedDelivery?.From, order.EstimatedDelivery?.To);
}

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.VariantIdsToCheckout).NotEmpty()
            .WithMessage("Selecciona al menos un ítem del carrito para hacer checkout.");
        RuleFor(x => x.ShippingAddress).NotEmpty()
            .WithMessage("La dirección de envío es obligatoria.");
        RuleFor(x => x.CouponCode).MaximumLength(30)
            .WithMessage("El código de cupón no puede superar 30 caracteres.");
    }
}

/// <summary>
/// Orquesta el primer tramo de la saga: Carrito → Inventario → Promociones (si hay cupón) → Pagos. Si CUALQUIER paso falla,
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
    private readonly ICouponServiceClient _couponClient;
    private readonly ILogger<CheckoutCommandHandler> _logger;

    public CheckoutCommandHandler(
        IOrderRepository orderRepository,
        ICartServiceClient cartClient,
        IInventoryServiceClient inventoryClient,
        IPaymentServiceClient paymentClient,
        ICouponServiceClient couponClient,
        ILogger<CheckoutCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _cartClient = cartClient;
        _inventoryClient = inventoryClient;
        _paymentClient = paymentClient;
        _couponClient = couponClient;
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

        var couponCode = string.IsNullOrWhiteSpace(request.CouponCode) ? null : request.CouponCode.Trim();
        var couponReserved = false;

        try
        {
            // Paso 3 (opcional): apartar un uso del cupón para ESTA orden. Promociones recalcula el
            // descuento con el subtotal real (no confiamos en el que mostró el navegador) y bloquea el
            // cupón mientras cuenta usos, así un cupón limitado nunca se usa de más.
            CouponReservation? coupon = null;
            if (couponCode is not null)
            {
                var subtotal = selectedItems.Sum(i => i.UnitPrice * i.Quantity);
                coupon = await _couponClient.ReserveAsync(orderId, couponCode, subtotal, request.AccessToken, ct);
                couponReserved = true;
            }

            // Paso 4: crear la orden con el stock (y el cupón) asegurados, usando el MISMO orderId con
            // el que se reservaron (ver el comentario en Order.Create para el porqué).
            var order = Order.Create(
                orderId,
                request.UserId,
                request.UserEmail,
                request.UserFullName,
                request.ShippingAddress,
                selectedItems.Select(i => (i.VariantId, i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity)),
                coupon is null ? null : (coupon.Code, coupon.DiscountAmount));

            // Paso 5: crear el pago en PayPal por el total de la orden (ya con el descuento).
            var paymentResult = await _paymentClient.CreatePaymentAsync(
                order.Id, order.TotalAmount, "USD", request.AccessToken, ct);

            await _orderRepository.AddAsync(order, ct);
            await _orderRepository.SaveChangesAsync(ct);

            return CheckoutResult.From(order, paymentResult.ApproveUrl);
        }
        catch (Exception)
        {
            // Compensación: si algo falla después de haber reservado stock (cupón rechazado, pago que
            // no se pudo crear...), hay que deshacer lo reservado — si no, el stock y el uso del cupón
            // quedan "atrapados" para siempre. Se deshace en orden inverso.
            _logger.LogWarning(
                "Falló el checkout de {OrderId} después de reservar stock — liberando lo reservado.", orderId);

            if (couponReserved)
            {
                try
                {
                    await _couponClient.ReleaseAsync(orderId, request.AccessToken, ct);
                }
                catch (Exception releaseEx)
                {
                    // No tapamos el error original; la reserva del cupón vence sola en 2 horas.
                    _logger.LogError(releaseEx, "No se pudo liberar el cupón de {OrderId}.", orderId);
                }
            }

            await _inventoryClient.ReleaseReservationAsync(orderId, request.AccessToken, ct);
            throw;
        }
    }
}

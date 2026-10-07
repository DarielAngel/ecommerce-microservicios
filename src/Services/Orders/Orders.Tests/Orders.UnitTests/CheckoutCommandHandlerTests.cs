using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class CheckoutCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly ICartServiceClient _cartClient = Substitute.For<ICartServiceClient>();
    private readonly IInventoryServiceClient _inventoryClient = Substitute.For<IInventoryServiceClient>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ICouponServiceClient _couponClient = Substitute.For<ICouponServiceClient>();

    private CheckoutCommandHandler CreateHandler() =>
        new(_orderRepository, _cartClient, _inventoryClient, _paymentClient, _couponClient, NullLogger<CheckoutCommandHandler>.Instance);

    private static CartItemInfo BuildCartItem(Guid variantId, int quantity = 2) =>
        new(variantId, Guid.NewGuid(), "Camiseta", "SKU-1", 20m, quantity);

    [Fact]
    public async Task Handle_ConStockYPagoOk_DeberiaCrearLaOrdenYDevolverElApproveUrl()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var cartItem = BuildCartItem(variantId);

        _cartClient.GetCartItemsAsync("token", Arg.Any<CancellationToken>()).Returns(new List<CartItemInfo> { cartItem });
        _inventoryClient.ReserveStockAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ReservationLineInput>>(), "token", Arg.Any<CancellationToken>())
            .Returns(true);
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), 40m, "USD", "token", Arg.Any<CancellationToken>())
            .Returns(new CreatePaymentResult("PendingApproval", "https://paypal.test/approve/X"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new CheckoutCommand(userId, "cliente@test.com", "Cliente Prueba", new List<Guid> { variantId }, "Calle Falsa 123", "token"), CancellationToken.None);

        result.ApproveUrl.Should().Be("https://paypal.test/approve/X");
        result.TotalAmount.Should().Be(40m);
        await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConItemSeleccionadoQueNoEstaEnElCarrito_DeberiaLanzarConflictAppException()
    {
        var userId = Guid.NewGuid();
        _cartClient.GetCartItemsAsync("token", Arg.Any<CancellationToken>()).Returns(new List<CartItemInfo>());

        var handler = CreateHandler();
        var act = async () => await handler.Handle(
            new CheckoutCommand(userId, "cliente@test.com", "Cliente Prueba", new List<Guid> { Guid.NewGuid() }, "Calle Falsa 123", "token"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
        await _inventoryClient.DidNotReceive().ReserveStockAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ReservationLineInput>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinStockSuficiente_DeberiaLanzarConflictAppExceptionYNoCrearNiPagoNiOrden()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        _cartClient.GetCartItemsAsync("token", Arg.Any<CancellationToken>()).Returns(new List<CartItemInfo> { BuildCartItem(variantId) });
        _inventoryClient.ReserveStockAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ReservationLineInput>>(), "token", Arg.Any<CancellationToken>())
            .Returns(false); // sin stock

        var handler = CreateHandler();
        var act = async () => await handler.Handle(
            new CheckoutCommand(userId, "cliente@test.com", "Cliente Prueba", new List<Guid> { variantId }, "Calle Falsa 123", "token"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
        await _paymentClient.DidNotReceive().CreatePaymentAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConStockOkPeroPagosFalla_DeberiaLiberarLaReservaYPropagarLaExcepcion()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        _cartClient.GetCartItemsAsync("token", Arg.Any<CancellationToken>()).Returns(new List<CartItemInfo> { BuildCartItem(variantId) });
        _inventoryClient.ReserveStockAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ReservationLineInput>>(), "token", Arg.Any<CancellationToken>())
            .Returns(true);
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), "token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Pagos no respondió"));

        var handler = CreateHandler();
        var act = async () => await handler.Handle(
            new CheckoutCommand(userId, "cliente@test.com", "Cliente Prueba", new List<Guid> { variantId }, "Calle Falsa 123", "token"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();

        // Esta es LA aserción clave de la compensación: si el pago falla después de reservar
        // stock, esa reserva NO puede quedar huérfana — tiene que liberarse.
        await _inventoryClient.Received(1).ReleaseReservationAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    // ---- Cupones ----

    private void GivenCartWithStock(Guid variantId)
    {
        _cartClient.GetCartItemsAsync("token", Arg.Any<CancellationToken>()).Returns(new List<CartItemInfo> { BuildCartItem(variantId) });
        _inventoryClient.ReserveStockAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ReservationLineInput>>(), "token", Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static CheckoutCommand CommandWithCoupon(Guid variantId, string? coupon) =>
        new(Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", new List<Guid> { variantId }, "Calle Falsa 123", "token", coupon);

    [Fact]
    public async Task Handle_ConCupon_DeberiaReservarloConElSubtotalRealYCobrarElTotalConDescuento()
    {
        var variantId = Guid.NewGuid();
        GivenCartWithStock(variantId); // 2 × $20 = $40
        _couponClient.ReserveAsync(Arg.Any<Guid>(), "verano10", 40m, "token", Arg.Any<CancellationToken>())
            .Returns(new CouponReservation("VERANO10", 4m));
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), "USD", "token", Arg.Any<CancellationToken>())
            .Returns(new CreatePaymentResult("PendingApproval", "https://paypal.test/approve/X"));

        var result = await CreateHandler().Handle(CommandWithCoupon(variantId, " verano10 "), CancellationToken.None);

        result.Subtotal.Should().Be(40m);
        result.DiscountAmount.Should().Be(4m);
        result.TotalAmount.Should().Be(36m);
        result.CouponCode.Should().Be("VERANO10");
        // PayPal cobra el total CON descuento, y el cupón se reservó con el MISMO id de la orden.
        await _paymentClient.Received(1).CreatePaymentAsync(result.OrderId, 36m, "USD", "token", Arg.Any<CancellationToken>());
        await _couponClient.Received(1).ReserveAsync(result.OrderId, "verano10", 40m, "token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinCupon_NoDeberiaLlamarAPromociones()
    {
        var variantId = Guid.NewGuid();
        GivenCartWithStock(variantId);
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), 40m, "USD", "token", Arg.Any<CancellationToken>())
            .Returns(new CreatePaymentResult("PendingApproval", "https://paypal.test/approve/X"));

        var result = await CreateHandler().Handle(CommandWithCoupon(variantId, "  "), CancellationToken.None);

        result.DiscountAmount.Should().Be(0);
        result.CouponCode.Should().BeNull();
        await _couponClient.DidNotReceiveWithAnyArgs().ReserveAsync(default, default!, default, default!, default);
    }

    [Fact]
    public async Task Handle_CuponRechazado_DeberiaLiberarElStockYNoCrearNiPagoNiOrden()
    {
        var variantId = Guid.NewGuid();
        GivenCartWithStock(variantId);
        _couponClient.ReserveAsync(Arg.Any<Guid>(), "AGOTADO", Arg.Any<decimal>(), "token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictAppException("Este cupón ya alcanzó su límite de usos."));

        var act = () => CreateHandler().Handle(CommandWithCoupon(variantId, "AGOTADO"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*límite de usos*");
        await _inventoryClient.Received(1).ReleaseReservationAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>());
        // El cupón nunca se reservó: no hay nada que liberar.
        await _couponClient.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default!, default);
        await _paymentClient.DidNotReceiveWithAnyArgs().CreatePaymentAsync(default, default, default!, default!, default);
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConCuponReservadoPeroPagosFalla_DeberiaLiberarElCuponYElStock()
    {
        var variantId = Guid.NewGuid();
        GivenCartWithStock(variantId);
        _couponClient.ReserveAsync(Arg.Any<Guid>(), "VERANO10", Arg.Any<decimal>(), "token", Arg.Any<CancellationToken>())
            .Returns(new CouponReservation("VERANO10", 4m));
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), "token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Pagos no respondió"));

        var act = () => CreateHandler().Handle(CommandWithCoupon(variantId, "VERANO10"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        await _couponClient.Received(1).ReleaseAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>());
        await _inventoryClient.Received(1).ReleaseReservationAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SiLiberarElCuponTambienFalla_DeberiaLiberarElStockYPropagarElErrorOriginal()
    {
        var variantId = Guid.NewGuid();
        GivenCartWithStock(variantId);
        _couponClient.ReserveAsync(Arg.Any<Guid>(), "VERANO10", Arg.Any<decimal>(), "token", Arg.Any<CancellationToken>())
            .Returns(new CouponReservation("VERANO10", 4m));
        _paymentClient.CreatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), "token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Pagos no respondió"));
        _couponClient.ReleaseAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Promociones tampoco"));

        var act = () => CreateHandler().Handle(CommandWithCoupon(variantId, "VERANO10"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Pagos no respondió");
        await _inventoryClient.Received(1).ReleaseReservationAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>());
    }
}

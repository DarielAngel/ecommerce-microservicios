using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class ConfirmPaymentCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IInventoryServiceClient _inventoryClient = Substitute.For<IInventoryServiceClient>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ICartServiceClient _cartClient = Substitute.For<ICartServiceClient>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ICouponServiceClient _couponClient = Substitute.For<ICouponServiceClient>();

    private ConfirmPaymentCommandHandler CreateHandler() =>
        new(_orderRepository, _inventoryClient, _paymentClient, _cartClient, _couponClient, _eventPublisher,
            NullLogger<ConfirmPaymentCommandHandler>.Instance);

    private static Order BuildPendingOrder(Guid orderId, Guid variantId) => Order.Create(
        orderId, Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123",
        new[] { (variantId, Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2) });

    [Fact]
    public async Task Handle_ConCapturaExitosa_DeberiaMarcarPagadaConfirmarStockYLimpiarElCarrito()
    {
        var orderId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var order = BuildPendingOrder(orderId, variantId);

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>())
            .Returns(new CapturePaymentResult(true, "Captured"));

        var handler = CreateHandler();
        var result = await handler.Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Paid");
        await _inventoryClient.Received(1).ConfirmReservationAsync(orderId, "token", Arg.Any<CancellationToken>());
        await _inventoryClient.DidNotReceive().ReleaseReservationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cartClient.Received(1).RemoveItemAsync(variantId, "token", Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<Ecommerce.Contracts.Events.OrderPaidEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConCapturaExitosa_DeberiaPublicarElEventoConLosIdsDeProductoSinRepetir()
    {
        var orderId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var order = Order.Create(
            orderId, Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123",
            new[]
            {
                (Guid.NewGuid(), productA, "Camiseta", "SKU-1", 20m, 1),
                (Guid.NewGuid(), productA, "Camiseta (otra talla)", "SKU-2", 20m, 1), // mismo producto, otra variante
                (Guid.NewGuid(), productB, "Gorra", "SKU-3", 10m, 1)
            });

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>())
            .Returns(new CapturePaymentResult(true, "Captured"));

        Ecommerce.Contracts.Events.OrderPaidEvent? published = null;
        await _eventPublisher.PublishAsync(
            Arg.Do<Ecommerce.Contracts.Events.OrderPaidEvent>(e => published = e), Arg.Any<CancellationToken>());

        await CreateHandler().Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        published.Should().NotBeNull();
        published!.ProductIds.Should().NotBeNull();
        published.ProductIds.Should().BeEquivalentTo(new[] { productA, productB }, "los productos se informan una sola vez aunque se compren dos variantes");
    }

    [Fact]
    public async Task Handle_ConCapturaFallida_DeberiaMarcarFallidaYLiberarElStock()
    {
        var orderId = Guid.NewGuid();
        var order = BuildPendingOrder(orderId, Guid.NewGuid());

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>())
            .Returns(new CapturePaymentResult(false, "Conflict"));

        var handler = CreateHandler();
        var result = await handler.Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Failed");
        await _inventoryClient.Received(1).ReleaseReservationAsync(orderId, "token", Arg.Any<CancellationToken>());
        await _inventoryClient.DidNotReceive().ConfirmReservationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cartClient.DidNotReceive().RemoveItemAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SobreUnaOrdenYaPagada_DeberiaSerIdempotenteYNoLlamarAPayPalDeNuevo()
    {
        var orderId = Guid.NewGuid();
        var order = BuildPendingOrder(orderId, Guid.NewGuid());
        order.MarkPaid();

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        var result = await handler.Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Paid");
        await _paymentClient.DidNotReceive().CapturePaymentAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConOrdenInexistente_DeberiaLanzarNotFoundAppException()
    {
        _orderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new ConfirmPaymentCommand(Guid.NewGuid(), "token"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    // ---- Cupones ----

    private static Order BuildPendingOrderWithCoupon(Guid orderId) => Order.Create(
        orderId, Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123",
        new[] { (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2) },
        ("VERANO10", 4m));

    [Fact]
    public async Task Handle_ConCapturaExitosaYCupon_DeberiaConfirmarElUsoDelCupon()
    {
        var orderId = Guid.NewGuid();
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(BuildPendingOrderWithCoupon(orderId));
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>()).Returns(new CapturePaymentResult(true, "COMPLETED"));

        var result = await CreateHandler().Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Paid");
        result.TotalAmount.Should().Be(36m);
        await _couponClient.Received(1).ConfirmAsync(orderId, "token", Arg.Any<CancellationToken>());
        await _couponClient.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_ConCapturaFallidaYCupon_DeberiaLiberarElUsoDelCupon()
    {
        var orderId = Guid.NewGuid();
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(BuildPendingOrderWithCoupon(orderId));
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>()).Returns(new CapturePaymentResult(false, "DECLINED"));

        var result = await CreateHandler().Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Failed");
        await _couponClient.Received(1).ReleaseAsync(orderId, "token", Arg.Any<CancellationToken>());
        await _couponClient.DidNotReceiveWithAnyArgs().ConfirmAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_ConCapturaExitosa_SiPromocionesFallaAlConfirmar_LaOrdenIgualQuedaPagada()
    {
        var orderId = Guid.NewGuid();
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(BuildPendingOrderWithCoupon(orderId));
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>()).Returns(new CapturePaymentResult(true, "COMPLETED"));
        _couponClient.ConfirmAsync(orderId, "token", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("caído"));

        var result = await CreateHandler().Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        result.Status.Should().Be("Paid", "el pago ya se capturó: un fallo de Promociones no puede revertirlo");
    }

    [Fact]
    public async Task Handle_SinCupon_NoDeberiaLlamarAPromociones()
    {
        var orderId = Guid.NewGuid();
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(BuildPendingOrder(orderId, Guid.NewGuid()));
        _paymentClient.CapturePaymentAsync(orderId, "token", Arg.Any<CancellationToken>()).Returns(new CapturePaymentResult(true, "COMPLETED"));

        await CreateHandler().Handle(new ConfirmPaymentCommand(orderId, "token"), CancellationToken.None);

        await _couponClient.DidNotReceiveWithAnyArgs().ConfirmAsync(default, default!, default);
    }
}

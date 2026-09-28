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

    private CheckoutCommandHandler CreateHandler() =>
        new(_orderRepository, _cartClient, _inventoryClient, _paymentClient, NullLogger<CheckoutCommandHandler>.Instance);

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
}

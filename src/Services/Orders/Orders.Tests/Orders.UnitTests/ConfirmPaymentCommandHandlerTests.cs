using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class ConfirmPaymentCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IInventoryServiceClient _inventoryClient = Substitute.For<IInventoryServiceClient>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ICartServiceClient _cartClient = Substitute.For<ICartServiceClient>();

    private ConfirmPaymentCommandHandler CreateHandler() =>
        new(_orderRepository, _inventoryClient, _paymentClient, _cartClient, NullLogger<ConfirmPaymentCommandHandler>.Instance);

    private static Order BuildPendingOrder(Guid orderId, Guid variantId) => Order.Create(
        orderId, Guid.NewGuid(), "Calle Falsa 123",
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
}

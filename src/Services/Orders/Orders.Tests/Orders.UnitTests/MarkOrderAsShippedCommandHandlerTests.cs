using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class MarkOrderAsShippedCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();

    private MarkOrderAsShippedCommandHandler CreateHandler() =>
        new(_orderRepository, _eventPublisher, NullLogger<MarkOrderAsShippedCommandHandler>.Instance);

    private static Order BuildPaidOrder(Guid orderId)
    {
        var order = Order.Create(
            orderId, Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123",
            new[] { (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2) });
        order.MarkPaid();
        return order;
    }

    [Fact]
    public async Task Handle_ConOrdenPagada_DeberiaMarcarEnviadaYPublicarElEvento()
    {
        var orderId = Guid.NewGuid();
        var order = BuildPaidOrder(orderId);
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        var result = await handler.Handle(new MarkOrderAsShippedCommand(orderId), CancellationToken.None);

        result.Status.Should().Be("Shipped");
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(Arg.Any<OrderShippedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LlamadoDosVeces_DeberiaSerIdempotenteYNoPublicarElEventoDeNuevo()
    {
        var orderId = Guid.NewGuid();
        var order = BuildPaidOrder(orderId);
        order.MarkShipped();
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        var result = await handler.Handle(new MarkOrderAsShippedCommand(orderId), CancellationToken.None);

        result.Status.Should().Be("Shipped");
        await _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<OrderShippedEvent>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConOrdenInexistente_DeberiaLanzarNotFoundAppException()
    {
        _orderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new MarkOrderAsShippedCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

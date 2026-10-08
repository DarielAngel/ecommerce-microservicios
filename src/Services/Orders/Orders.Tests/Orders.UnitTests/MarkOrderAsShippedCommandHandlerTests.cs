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
    private readonly IOrderLock _orderLock = Substitute.For<IOrderLock>();

    public MarkOrderAsShippedCommandHandlerTests()
    {
        _orderLock.AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Substitute.For<IAsyncDisposable>()));
    }

    private MarkOrderAsShippedCommandHandler CreateHandler() =>
        new(_orderRepository, _orderLock, _eventPublisher, NullLogger<MarkOrderAsShippedCommandHandler>.Instance);

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
        _orderRepository.GetByIdFreshAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

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
        _orderRepository.GetByIdFreshAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        var result = await handler.Handle(new MarkOrderAsShippedCommand(orderId), CancellationToken.None);

        result.Status.Should().Be("Shipped");
        await _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<OrderShippedEvent>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConOrdenInexistente_DeberiaLanzarNotFoundAppException()
    {
        _orderRepository.GetByIdFreshAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new MarkOrderAsShippedCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Handle_ConUnaCancelacionPedida_NoEnviaYLoHaceBajoElCandado()
    {
        var orderId = Guid.NewGuid();
        var order = BuildPaidOrder(orderId);
        order.RequestCancellation(ReturnReason.ChangedMind, null, DateTime.UtcNow);
        _orderRepository.GetByIdFreshAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var act = () => CreateHandler().Handle(new MarkOrderAsShippedCommand(orderId), CancellationToken.None);

        await act.Should().ThrowAsync<Ecommerce.Orders.Domain.Exceptions.DomainException>();
        await _orderLock.Received(1).AcquireAsync(orderId, Arg.Any<CancellationToken>());
        await _eventPublisher.DidNotReceiveWithAnyArgs().PublishAsync<OrderShippedEvent>(default!, default);
    }
}

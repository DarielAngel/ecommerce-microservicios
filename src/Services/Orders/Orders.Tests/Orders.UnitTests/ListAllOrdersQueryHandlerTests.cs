using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class ListAllOrdersQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_DeberiaIncluirDatosDelCompradorYDeLaOrden()
    {
        var order = Order.Create(
            Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123",
            new[] { (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2) });

        _orderRepository.ListAllAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Order> { order });

        var handler = new ListAllOrdersQueryHandler(_orderRepository);
        var result = await handler.Handle(new ListAllOrdersQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].UserEmail.Should().Be("cliente@test.com");
        result[0].TotalAmount.Should().Be(40m);
    }

    [Fact]
    public async Task Handle_ConCountFueraDeRango_DeberiaAcotarloEntre1Y500()
    {
        await _orderRepository.DidNotReceive().ListAllAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _orderRepository.ListAllAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<Order>());

        var handler = new ListAllOrdersQueryHandler(_orderRepository);
        await handler.Handle(new ListAllOrdersQuery(Count: 99999), CancellationToken.None);

        await _orderRepository.Received(1).ListAllAsync(500, Arg.Any<CancellationToken>());
    }
}

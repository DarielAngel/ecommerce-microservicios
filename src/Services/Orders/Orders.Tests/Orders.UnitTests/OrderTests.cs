using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using Ecommerce.Orders.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class OrderTests
{
    private static readonly (Guid, Guid, string, string, decimal, int) SampleItem =
        (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2);

    [Fact]
    public void Create_ConDatosValidos_DeberiaQuedarPendingPaymentConElMismoId()
    {
        var orderId = Guid.NewGuid();

        var order = Order.Create(orderId, Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });

        order.Id.Should().Be(orderId, "el Id debe ser el mismo que se usó para reservar stock antes de crear la orden");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.TotalAmount.Should().Be(40m);
    }

    [Fact]
    public void Create_SinItems_DeberiaLanzarDomainException()
    {
        var act = () => Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", Array.Empty<(Guid, Guid, string, string, decimal, int)>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SinDireccionDeEnvio_DeberiaLanzarDomainException()
    {
        var act = () => Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "  ", new[] { SampleItem });

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkPaid_DesdePendingPayment_DeberiaFuncionar()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });

        order.MarkPaid();

        order.Status.Should().Be(OrderStatus.Paid);
        order.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_DespuesDePagada_DeberiaLanzarDomainException()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });
        order.MarkPaid();

        var act = () => order.MarkFailed("no debería poder");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkCancelled_DespuesDePagada_DeberiaLanzarDomainException()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });
        order.MarkPaid();

        var act = () => order.MarkCancelled();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkShipped_DesdePaid_DeberiaFuncionar()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });
        order.MarkPaid();

        order.MarkShipped();

        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void MarkShipped_SinEstarPagada_DeberiaLanzarDomainException()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente Prueba", "Calle Falsa 123", new[] { SampleItem });

        var act = () => order.MarkShipped();

        act.Should().Throw<DomainException>();
    }
}

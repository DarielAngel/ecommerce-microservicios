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

    // ---- Cupón ----

    private static Order CreateWithCoupon((string Code, decimal DiscountAmount)? coupon) => Order.Create(
        Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente", "Calle Falsa 123",
        new[] { (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 25m, 2) }, coupon);

    [Fact]
    public void Create_ConCupon_DeberiaRestarElDescuentoDelTotal()
    {
        var order = CreateWithCoupon(("verano10", 5m));

        order.Subtotal.Should().Be(50m);
        order.DiscountAmount.Should().Be(5m);
        order.TotalAmount.Should().Be(45m);
        order.CouponCode.Should().Be("VERANO10");
    }

    [Fact]
    public void Create_SinCupon_ElTotalEsElSubtotal()
    {
        var order = CreateWithCoupon(null);

        order.TotalAmount.Should().Be(order.Subtotal);
        order.DiscountAmount.Should().Be(0);
        order.CouponCode.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50)]
    [InlineData(60)]
    public void Create_ConDescuentoQueNoDejaAlgoPorCobrar_DeberiaRechazarse(decimal discount)
    {
        var act = () => CreateWithCoupon(("X10", discount));
        act.Should().Throw<DomainException>().WithMessage("*descuento*");
    }

    // ---- Puntos (Fase 6) ----

    private static Order CreateWith((string, decimal)? coupon, (int, decimal)? loyalty) => Order.Create(
        Guid.NewGuid(), Guid.NewGuid(), "cliente@test.com", "Cliente", "Calle 1",
        new[] { (Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "SKU-1", 20m, 2) }, coupon, loyalty);

    [Fact]
    public void ConPuntos_ElTotalDescuentaCuponYPuntos()
    {
        var order = CreateWith(("VERANO10", 4m), (500, 5m));

        order.LoyaltyPoints.Should().Be(500);
        order.TotalAmount.Should().Be(31m);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(100, 0)]
    public void PuntosInvalidos_SeRechazan(int points, decimal discount)
    {
        var act = () => CreateWith(null, (points, discount));
        act.Should().Throw<Ecommerce.Orders.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public void LosPuntosNoPuedenDejarLaOrdenEnCero()
    {
        var act = () => CreateWith(("VERANO10", 30m), (1000, 10m));
        act.Should().Throw<Ecommerce.Orders.Domain.Exceptions.DomainException>().WithMessage("*menor que lo que queda por pagar*");
    }
}

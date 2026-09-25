using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Inventory.UnitTests;

public class StockReservationTests
{
    [Fact]
    public void Create_ConItemsValidos_DeberiaCrearseEnEstadoActive()
    {
        var orderId = Guid.NewGuid();
        var items = new[] { (VariantId: Guid.NewGuid(), Quantity: 2) };

        var reservation = StockReservation.Create(orderId, items);

        reservation.OrderId.Should().Be(orderId);
        reservation.Status.Should().Be(ReservationStatus.Active);
        reservation.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void Create_SinItems_DeberiaLanzarDomainException()
    {
        var act = () => StockReservation.Create(Guid.NewGuid(), Array.Empty<(Guid, int)>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ConCantidadCero_DeberiaLanzarDomainException()
    {
        var items = new[] { (VariantId: Guid.NewGuid(), Quantity: 0) };

        var act = () => StockReservation.Create(Guid.NewGuid(), items);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_DesdeActive_DeberiaCambiarEstadoYRegistrarFecha()
    {
        var reservation = StockReservation.Create(Guid.NewGuid(), new[] { (Guid.NewGuid(), 1) });

        reservation.Confirm();

        reservation.Status.Should().Be(ReservationStatus.Confirmed);
        reservation.ConfirmedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Confirm_DosVeces_DeberiaLanzarDomainException()
    {
        var reservation = StockReservation.Create(Guid.NewGuid(), new[] { (Guid.NewGuid(), 1) });
        reservation.Confirm();

        var act = () => reservation.Confirm();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Release_DespuesDeConfirmada_DeberiaLanzarDomainException()
    {
        var reservation = StockReservation.Create(Guid.NewGuid(), new[] { (Guid.NewGuid(), 1) });
        reservation.Confirm();

        var act = () => reservation.Release();

        act.Should().Throw<DomainException>();
    }
}

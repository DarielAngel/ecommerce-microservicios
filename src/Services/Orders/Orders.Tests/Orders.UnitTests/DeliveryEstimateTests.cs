using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class DeliveryEstimateTests
{
    // Miércoles 7 de octubre de 2026.
    private static readonly DateTime Wednesday = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);
    // Viernes 9 de octubre de 2026.
    private static readonly DateTime Friday = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, "2026-10-08")] // jueves
    [InlineData(2, "2026-10-09")] // viernes
    [InlineData(3, "2026-10-12")] // salta sábado y domingo → lunes
    [InlineData(6, "2026-10-15")]
    public void AddBusinessDays_SaltaLosFinesDeSemana(int days, string expected)
    {
        DeliveryEstimate.AddBusinessDays(Wednesday, days).Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void Pagado_EntreTresYSeisDiasHabilesDesdeElPago()
    {
        var estimate = DeliveryEstimate.For(OrderStatus.Paid, Wednesday, null);

        estimate.Should().Be(new DeliveryEstimate(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Enviado_EntreUnoYTresDiasHabilesDesdeElEnvio()
    {
        var estimate = DeliveryEstimate.For(OrderStatus.Shipped, Wednesday, Friday);

        estimate.Should().Be(new DeliveryEstimate(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 14)));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Failed)]
    [InlineData(OrderStatus.Cancelled)]
    public void SinPago_NoHayEstimacion(OrderStatus status)
    {
        DeliveryEstimate.For(status, null, null).Should().BeNull();
    }

    [Fact]
    public void MarcarComoEnviada_GuardaLaFechaYLaEstimacionPasaAContarDesdeElEnvio()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "ana@ejemplo.com", "Ana", "Calle 1",
            [(Guid.NewGuid(), Guid.NewGuid(), "Taza", "TZ-1", 8m, 1)]);
        order.MarkPaid();
        order.EstimatedDelivery.Should().NotBeNull();

        order.MarkShipped();

        order.ShippedAtUtc.Should().NotBeNull();
        order.ShippedAtUtc.Should().BeOnOrAfter(order.PaidAtUtc!.Value);
        order.EstimatedDelivery!.Value.From.Should().Be(
            DeliveryEstimate.AddBusinessDays(order.ShippedAtUtc!.Value, DeliveryEstimate.ShippedMinDays));
    }

    [Fact]
    public void UnaOrdenPendiente_NoTieneEstimacion()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "ana@ejemplo.com", "Ana", "Calle 1",
            [(Guid.NewGuid(), Guid.NewGuid(), "Taza", "TZ-1", 8m, 1)]);

        order.EstimatedDelivery.Should().BeNull();
    }
}

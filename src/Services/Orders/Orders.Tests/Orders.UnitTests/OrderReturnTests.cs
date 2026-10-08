using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using Ecommerce.Orders.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class OrderReturnTests
{
    private static readonly Guid Mug = Guid.NewGuid();
    private static readonly Guid Plate = Guid.NewGuid();

    /// <summary>Taza 2 × $20 + Plato 1 × $10 = $50; cupón $5 y 500 puntos ($5): se cobraron $40.</summary>
    private static Order ShippedOrder(bool withDiscounts = true)
    {
        var order = Order.Create(
            Guid.NewGuid(), Guid.NewGuid(), "ana@test.com", "Ana", "Calle 1",
            new[] { (Mug, Guid.NewGuid(), "Taza", "TZ", 20m, 2), (Plate, Guid.NewGuid(), "Plato", "PL", 10m, 1) },
            withDiscounts ? ("PROMO5", 5m) : null,
            withDiscounts ? (500, 5m) : null);
        order.MarkPaid();
        order.MarkShipped();
        return order;
    }

    private static DateTime SoonAfter(Order o) => o.ShippedAtUtc!.Value.AddDays(2);

    private static OrderReturn Request(Order order, params (Guid, int)[] items) =>
        order.RequestReturn(items, ReturnReason.Damaged, "Llegó roto", SoonAfter(order));

    [Fact]
    public void SoloSePuedeDevolverUnPedidoEnviado()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "a@t.com", "A", "C", new[] { (Mug, Guid.NewGuid(), "Taza", "TZ", 20m, 1) });
        order.MarkPaid();

        order.WhyCannotRequestReturn(DateTime.UtcNow).Should().Contain("enviados");
        var act = () => order.RequestReturn(new[] { (Mug, 1) }, ReturnReason.Damaged, null, DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ElPlazoEsDe30DiasDesdeElEnvio()
    {
        var order = ShippedOrder();

        order.ReturnDeadlineUtc.Should().Be(order.ShippedAtUtc!.Value.AddDays(30));
        order.WhyCannotRequestReturn(order.ShippedAtUtc.Value.AddDays(30)).Should().BeNull();
        var late = () => order.RequestReturn(new[] { (Mug, 1) }, ReturnReason.Damaged, null, order.ShippedAtUtc.Value.AddDays(31));
        late.Should().Throw<DomainException>().WithMessage("*30 días*");
    }

    [Fact]
    public void NoSePuedeDevolverMasDeLoComprado_NiProductosDeOtroPedido()
    {
        var order = ShippedOrder();

        ((Action)(() => Request(order, (Mug, 3)))).Should().Throw<DomainException>().WithMessage("*como máximo 2*");
        ((Action)(() => Request(order, (Guid.NewGuid(), 1)))).Should().Throw<DomainException>().WithMessage("*no es de este pedido*");
        order.Returns.Should().BeEmpty();
    }

    [Fact]
    public void SoloUnaDevolucionAbiertaALaVez_YLasRechazadasLiberanLasUnidades()
    {
        var order = ShippedOrder();
        var first = Request(order, (Mug, 2));

        order.ReturnableQuantity(Mug).Should().Be(0);
        ((Action)(() => Request(order, (Plate, 1)))).Should().Throw<DomainException>().WithMessage("*en curso*");

        order.RejectReturn(first.Id, "No aceptamos productos usados.", SoonAfter(order));
        order.ReturnableQuantity(Mug).Should().Be(2, "una devolución rechazada no se lleva las unidades");
        Request(order, (Mug, 1)).Status.Should().Be(ReturnStatus.Requested);
    }

    [Fact]
    public void ElMotivoOtroNecesitaComentario()
    {
        var order = ShippedOrder();

        var act = () => order.RequestReturn(new[] { (Mug, 1) }, ReturnReason.Other, "  ", SoonAfter(order));

        act.Should().Throw<DomainException>().WithMessage("*motivo*");
    }

    [Fact]
    public void ElReembolsoEsProporcionalALoPagado_YLaUltimaDevolucionCompletaExactamenteLoCobrado()
    {
        var order = ShippedOrder();
        order.TotalAmount.Should().Be(40m);

        // 1 taza = $20 de $50 → 40 % de lo pagado ($40) y de los puntos usados (500).
        var first = Request(order, (Mug, 1));
        order.QuoteRefund(first.Id).Should().Be(new RefundQuote(16m, 200, false));
        order.ApproveReturn(first.Id, null, SoonAfter(order)).RefundAmount.Should().Be(16m);
        order.MarkReturnRefunded(first.Id, SoonAfter(order));
        order.Status.Should().Be(OrderStatus.Shipped, "todavía quedan productos sin devolver");
        order.RefundedAmount.Should().Be(16m);

        // El resto: se lleva exactamente lo que queda, ni un centavo más.
        var rest = Request(order, (Mug, 1), (Plate, 1));
        order.QuoteRefund(rest.Id).Should().Be(new RefundQuote(24m, 300, true));
        order.ApproveReturn(rest.Id, "Recibido", SoonAfter(order));
        order.MarkReturnRefunded(rest.Id, SoonAfter(order));

        order.RefundedAmount.Should().Be(order.TotalAmount);
        order.Status.Should().Be(OrderStatus.Refunded);
        order.WhyCannotRequestReturn(SoonAfter(order)).Should().Contain("completo");
    }

    [Fact]
    public void LosRedondeosNuncaSumanMasQueLoCobrado()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "a@t.com", "A", "C",
            new[] { (Mug, Guid.NewGuid(), "Taza", "TZ", 10m, 3) }, ("UNO", 1m));
        order.MarkPaid();
        order.MarkShipped();
        var refunded = new List<decimal>();

        for (var i = 0; i < 3; i++)
        {
            var r = Request(order, (Mug, 1));
            refunded.Add(order.ApproveReturn(r.Id, null, SoonAfter(order)).RefundAmount);
            order.MarkReturnRefunded(r.Id, SoonAfter(order));
        }

        refunded.Should().Equal(9.67m, 9.67m, 9.66m);
        refunded.Sum().Should().Be(29m);
    }

    [Fact]
    public void AprobarDosVeces_NoCambiaElMonto_YNoSePuedeRechazarUnaAprobada()
    {
        var order = ShippedOrder();
        var r = Request(order, (Plate, 1));

        var amount = order.ApproveReturn(r.Id, null, SoonAfter(order)).RefundAmount;
        order.ApproveReturn(r.Id, null, SoonAfter(order)).RefundAmount.Should().Be(amount);

        var reject = () => order.RejectReturn(r.Id, "No", SoonAfter(order));
        reject.Should().Throw<DomainException>().WithMessage("*ya fue aprobada*");
    }

    [Fact]
    public void RechazarPideNota_YUnaRechazadaNoSePuedeAprobar()
    {
        var order = ShippedOrder();
        var r = Request(order, (Plate, 1));

        ((Action)(() => order.RejectReturn(r.Id, " ", SoonAfter(order)))).Should().Throw<DomainException>().WithMessage("*nota*");
        order.RejectReturn(r.Id, "Fuera de política", SoonAfter(order)).AdminNote.Should().Be("Fuera de política");

        ((Action)(() => order.ApproveReturn(r.Id, null, SoonAfter(order)))).Should().Throw<DomainException>().WithMessage("*rechazada*");
    }

    [Fact]
    public void SinCuponNiPuntos_SeDevuelveElPrecioDeLoDevuelto()
    {
        var order = ShippedOrder(withDiscounts: false);
        var r = Request(order, (Mug, 1));

        order.QuoteRefund(r.Id).Should().Be(new RefundQuote(20m, 0, false));
    }

    [Fact]
    public void SoloSeReembolsaUnaDevolucionAprobada()
    {
        var order = ShippedOrder();
        var r = Request(order, (Mug, 1));

        var act = () => order.MarkReturnRefunded(r.Id, SoonAfter(order));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UnaAprobadaSoloSeCancelaSiPagosConfirmoQueNoHuboReembolso()
    {
        var order = ShippedOrder();
        var r = Request(order, (Mug, 1));
        order.ApproveReturn(r.Id, null, SoonAfter(order));

        ((Action)(() => order.RejectReturn(r.Id, "No", SoonAfter(order)))).Should().Throw<DomainException>();
        order.RejectReturn(r.Id, "PayPal lo rechazó", SoonAfter(order), approvedWithoutRefund: true).Status.Should().Be(ReturnStatus.Rejected);
        order.WhyCannotRequestReturn(SoonAfter(order)).Should().BeNull("ya no hay una devolución en curso");
    }

    [Fact]
    public void ElPlazoNoSeCorreConLasDevoluciones()
    {
        var order = ShippedOrder();
        var deadline = order.ReturnDeadlineUtc;

        var r = Request(order, (Mug, 1));
        order.RejectReturn(r.Id, "No", order.ShippedAtUtc!.Value.AddDays(20));

        order.ReturnDeadlineUtc.Should().Be(deadline);
    }
}

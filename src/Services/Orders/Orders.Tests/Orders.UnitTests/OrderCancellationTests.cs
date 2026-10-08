using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using Ecommerce.Orders.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class OrderCancellationTests
{
    private static readonly Guid Mug = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Taza 2 × $20 con cupón $4 y 400 puntos ($4): se cobraron $32.</summary>
    private static Order PaidOrder()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "a@t.com", "Ana", "Calle 1",
            new[] { (Mug, Guid.NewGuid(), "Taza", "TZ", 20m, 2) }, ("PROMO", 4m), (400, 4m));
        order.MarkPaid();
        return order;
    }

    [Fact]
    public void UnPedidoSinPagar_SeCancelaAlInstante()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "a@t.com", "Ana", "C", new[] { (Mug, Guid.NewGuid(), "Taza", "TZ", 20m, 1) });

        order.WhyCannotCancel().Should().BeNull();
        order.MarkCancelled(Now);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAtUtc.Should().Be(Now);
    }

    [Fact]
    public void PagadoSinEnviar_SePideLaCancelacionDeTodo_YMientrasTantoNoSePuedeEnviar()
    {
        var order = PaidOrder();

        var cancellation = order.RequestCancellation(ReturnReason.ChangedMind, "Me equivoqué de talla", Now);

        cancellation.IsCancellation.Should().BeTrue();
        cancellation.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
        order.Status.Should().Be(OrderStatus.Paid);
        order.WhyCannotCancel().Should().Contain("Ya pediste");
        ((Action)order.MarkShipped).Should().Throw<DomainException>().WithMessage("*cancelar este pedido*");
    }

    [Fact]
    public void AlAprobarYReembolsar_SeDevuelveTodo_YElPedidoQuedaCancelado()
    {
        var order = PaidOrder();
        var cancellation = order.RequestCancellation(ReturnReason.ChangedMind, null, Now);

        order.QuoteRefund(cancellation.Id).Should().Be(new RefundQuote(32m, 400, true));
        order.ApproveReturn(cancellation.Id, null, Now);
        order.MarkReturnRefunded(cancellation.Id, Now);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAtUtc.Should().Be(Now);
        order.RefundedAmount.Should().Be(32m);
        order.WhyCannotCancel().Should().NotBeNull();
    }

    [Fact]
    public void SiSeRechaza_ElPedidoSigueSuCurso()
    {
        var order = PaidOrder();
        var cancellation = order.RequestCancellation(ReturnReason.ChangedMind, null, Now);

        order.RejectReturn(cancellation.Id, "Ya salió del depósito", Now);
        order.MarkShipped();

        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void UnPedidoEnviado_NoSeCancela_SeDevuelve()
    {
        var order = PaidOrder();
        order.MarkShipped();

        order.WhyCannotCancel().Should().Contain("pide una devolución");
        ((Action)(() => order.RequestCancellation(ReturnReason.ChangedMind, null, Now))).Should().Throw<DomainException>();
        ((Action)(() => order.MarkCancelled(Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void UnaCancelacionReembolsadaSobreUnPedidoYaEnviado_NoLoMarcaCanceladoSinoReembolsado()
    {
        var order = PaidOrder();
        var cancellation = order.RequestCancellation(ReturnReason.ChangedMind, null, Now);
        order.ApproveReturn(cancellation.Id, null, Now);
        // (Simula un pedido que quedó enviado por una carrera vieja: no debe pisarse el estado.)
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, OrderStatus.Shipped);

        order.MarkReturnRefunded(cancellation.Id, Now);

        order.Status.Should().Be(OrderStatus.Refunded);
    }
}

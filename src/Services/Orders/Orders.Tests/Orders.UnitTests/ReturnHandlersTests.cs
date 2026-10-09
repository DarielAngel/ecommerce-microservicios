using Ecommerce.Contracts.Events;
using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecommerce.Orders.UnitTests;

public class ReturnHandlersTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IOrderLock _lock = Substitute.For<IOrderLock>();
    private readonly IPaymentServiceClient _payments = Substitute.For<IPaymentServiceClient>();
    private readonly IEventPublisher _events = Substitute.For<IEventPublisher>();
    private readonly Guid _mug = Guid.NewGuid();

    public ReturnHandlersTests()
    {
        _lock.AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Substitute.For<IAsyncDisposable>()));
    }

    private Order GivenShippedOrder(Guid? userId = null, string? coupon = null)
    {
        var order = Order.Create(Guid.NewGuid(), userId ?? Guid.NewGuid(), "ana@test.com", "Ana", "Calle 1",
            new[] { (_mug, Guid.NewGuid(), "Taza", "TZ", 20m, 2) },
            coupon is null ? null : (coupon, 4m));
        order.MarkPaid();
        order.MarkShipped();
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _orders.GetByIdFreshAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        return order;
    }

    private OrderReturn GivenRequestedReturn(Order order, int quantity = 1)
    {
        var r = order.RequestReturn(new[] { (_mug, quantity) }, ReturnReason.Damaged, null, DateTime.UtcNow);
        _orders.FindOrderIdByReturnIdAsync(r.Id, Arg.Any<CancellationToken>()).Returns(order.Id);
        return r;
    }

    private RejectReturnCommandHandler Rejecter() =>
        new(_orders, _lock, _payments, _events, TimeProvider.System, NullLogger<RejectReturnCommandHandler>.Instance);

    private ApproveReturnCommandHandler Approver() =>
        new(_orders, _lock, _payments, _events, TimeProvider.System, NullLogger<ApproveReturnCommandHandler>.Instance);

    [Fact]
    public async Task Pedir_UnPedidoAjeno_EsNotFound()
    {
        var order = GivenShippedOrder();
        var handler = new RequestReturnCommandHandler(_orders, _lock, TimeProvider.System);

        var act = () => handler.Handle(new RequestReturnCommand(order.Id, Guid.NewGuid(), new[] { new ReturnItemInput(_mug, 1) }, "Damaged", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
        order.Returns.Should().BeEmpty();
    }

    [Fact]
    public async Task Pedir_DevuelveElPedidoConLaDevolucionYLasUnidadesQueQuedan()
    {
        var userId = Guid.NewGuid();
        var order = GivenShippedOrder(userId);
        var handler = new RequestReturnCommandHandler(_orders, _lock, TimeProvider.System);

        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, new[] { new ReturnItemInput(_mug, 1) }, "doesnotfit", "Grande"), CancellationToken.None);

        result.Returns.Should().ContainSingle().Which.Should().Match<ReturnResult>(r => r.Status == "Requested" && r.Reason == "DoesNotFit");
        result.Lines.Single().ReturnableQuantity.Should().Be(1);
        result.CanRequestReturn.Should().BeFalse("ya hay una en curso");
        await _orders.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_ReembolsaElMontoFijado_MarcaReembolsadaYPublicaElEvento()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order, 2);
        _payments.RefundAsync(order.Id, r.Id, 40m, Arg.Any<string?>(), "admin-token", Arg.Any<CancellationToken>())
            .Returns(new RefundPaymentResult(r.Id, 40m, "PP-R", "Refunded"));

        var result = await Approver().Handle(new ApproveReturnCommand(r.Id, null, "admin-token"), CancellationToken.None);

        result.Status.Should().Be("Refunded");
        result.RefundAmount.Should().Be(40m);
        order.Status.Should().Be(OrderStatus.Refunded);
        await _orders.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>()); // aprobada (antes de pagar) y reembolsada
        await _events.Received(1).PublishAsync(Arg.Is<OrderRefundedEvent>(e =>
            e.ReturnId == r.Id && e.RefundAmount == 40m && e.OrderFullyRefunded && e.Items.Single().Quantity == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_LaQueCompletaElPedido_ConCupon_MandaElCodigoParaLiberarlo()
    {
        var order = GivenShippedOrder(coupon: "verano25");
        var r = GivenRequestedReturn(order, 2);
        _payments.RefundAsync(default, default, default, default, default!, default)
            .ReturnsForAnyArgs(new RefundPaymentResult(r.Id, 36m, "PP-R", "Refunded"));

        await Approver().Handle(new ApproveReturnCommand(r.Id, null, "t"), CancellationToken.None);

        await _events.Received(1).PublishAsync(Arg.Is<OrderRefundedEvent>(e =>
            e.OrderFullyRefunded && e.CouponCode == "VERANO25"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_UnaParcial_ConCupon_NoLiberaElCupon()
    {
        var order = GivenShippedOrder(coupon: "VERANO25");
        var r = GivenRequestedReturn(order, 1);
        _payments.RefundAsync(default, default, default, default, default!, default)
            .ReturnsForAnyArgs(new RefundPaymentResult(r.Id, 18m, "PP-R", "Refunded"));

        await Approver().Handle(new ApproveReturnCommand(r.Id, null, "t"), CancellationToken.None);

        await _events.Received(1).PublishAsync(Arg.Is<OrderRefundedEvent>(e =>
            !e.OrderFullyRefunded && e.CouponCode == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_SiPagosRechaza_QuedaAprobadaGuardadaYSinEvento()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order);
        _payments.RefundAsync(default, default, default, default, default!, default)
            .ReturnsForAnyArgs<RefundPaymentResult>(_ => throw new ConflictAppException("PayPal rechazó el reembolso (422)."));

        var act = () => Approver().Handle(new ApproveReturnCommand(r.Id, null, "t"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*422*puedes reintentar o cancelarla*");
        r.Status.Should().Be(ReturnStatus.Approved);
        r.RefundAmount.Should().Be(20m);
        await _orders.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _events.DidNotReceiveWithAnyArgs().PublishAsync<OrderRefundedEvent>(default!, default);
    }

    [Fact]
    public async Task Aprobar_UnaYaReembolsada_NoVuelveACobrarSoloReenviaElEvento()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order);
        order.ApproveReturn(r.Id, null, DateTime.UtcNow);
        order.MarkReturnRefunded(r.Id, DateTime.UtcNow);

        await Approver().Handle(new ApproveReturnCommand(r.Id, null, "t"), CancellationToken.None);

        await _payments.DidNotReceiveWithAnyArgs().RefundAsync(default, default, default, default, default!, default);
        await _events.Received(1).PublishAsync(Arg.Is<OrderRefundedEvent>(e => e.ReturnId == r.Id && !e.OrderFullyRefunded), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rechazar_GuardaLaNotaYAvisaAlCliente_UnaSolaVez()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order);
        var handler = Rejecter();

        var result = await handler.Handle(new RejectReturnCommand(r.Id, "Producto usado"), CancellationToken.None);
        await handler.Handle(new RejectReturnCommand(r.Id, "Producto usado"), CancellationToken.None);

        result.Status.Should().Be("Rejected");
        result.RefundAmount.Should().Be(0);
        await _events.Received(1).PublishAsync(Arg.Is<ReturnRejectedEvent>(e => e.Note == "Producto usado"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelar_UnaAprobadaCuyoReembolsoNuncaSeHizo_LaRechaza()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order);
        order.ApproveReturn(r.Id, null, DateTime.UtcNow);
        _payments.FindRefundAsync(order.Id, r.Id, "t", Arg.Any<CancellationToken>()).Returns((RefundPaymentResult?)null);

        var result = await Rejecter().Handle(new RejectReturnCommand(r.Id, "PayPal no permite reembolsar", "t"), CancellationToken.None);

        result.Status.Should().Be("Rejected");
        order.ReturnableQuantity(_mug).Should().Be(2);
    }

    [Fact]
    public async Task Cancelar_UnaAprobadaQueSiSeReembolso_LaDaPorReembolsadaYAvisa()
    {
        var order = GivenShippedOrder();
        var r = GivenRequestedReturn(order);
        order.ApproveReturn(r.Id, null, DateTime.UtcNow);
        _payments.FindRefundAsync(order.Id, r.Id, "t", Arg.Any<CancellationToken>()).Returns(new RefundPaymentResult(r.Id, 20m, "PP-R", "Captured"));

        var act = () => Rejecter().Handle(new RejectReturnCommand(r.Id, "No", "t"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*sí se hizo*");
        r.Status.Should().Be(ReturnStatus.Refunded);
        await _events.Received(1).PublishAsync(Arg.Is<OrderRefundedEvent>(e => e.ReturnId == r.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_UnaDevolucionInexistente_EsNotFound()
    {
        var act = () => Approver().Handle(new ApproveReturnCommand(Guid.NewGuid(), null, "t"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Theory]
    [InlineData("Damaged", true)]
    [InlineData("other", true)]
    [InlineData("1", false)]
    [InlineData("Roto", false)]
    [InlineData("", false)]
    public void Validador_SoloAceptaMotivosConocidos(string reason, bool valid)
    {
        new RequestReturnCommandValidator()
            .Validate(new RequestReturnCommand(Guid.NewGuid(), Guid.NewGuid(), new[] { new ReturnItemInput(Guid.NewGuid(), 1) }, reason, null))
            .IsValid.Should().Be(valid);
    }

    // ---- Cancelar (Fase 7) ----

    private readonly IInventoryServiceClient _inventory = Substitute.For<IInventoryServiceClient>();
    private readonly ICouponServiceClient _coupons = Substitute.For<ICouponServiceClient>();
    private readonly ILoyaltyServiceClient _loyalty = Substitute.For<ILoyaltyServiceClient>();
    private readonly MediatR.ISender _mediator = Substitute.For<MediatR.ISender>();

    private CancelOrderCommandHandler Canceller() => new(
        _orders, _lock, _inventory, _coupons, _loyalty, _payments, _mediator, TimeProvider.System, NullLogger<CancelOrderCommandHandler>.Instance);

    private Order GivenOrder(Guid userId, bool paid)
    {
        var order = Order.Create(Guid.NewGuid(), userId, "ana@test.com", "Ana", "Calle 1",
            new[] { (_mug, Guid.NewGuid(), "Taza", "TZ", 20m, 1) }, ("PROMO", 2m));
        if (paid) order.MarkPaid();
        _orders.GetByIdFreshAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        return order;
    }

    [Fact]
    public async Task Cancelar_SinPagar_LiberaStockYCuponYQuedaCancelada()
    {
        var user = Guid.NewGuid();
        var order = GivenOrder(user, paid: false);

        var result = await Canceller().Handle(new CancelOrderCommand(order.Id, user, false, "ChangedMind", null, "t"), CancellationToken.None);

        result.Status.Should().Be("Cancelled");
        await _inventory.Received(1).ReleaseReservationAsync(order.Id, "t", Arg.Any<CancellationToken>());
        await _coupons.Received(1).ReleaseAsync(order.Id, "t", Arg.Any<CancellationToken>());
        await _loyalty.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default!, default);
    }

    [Fact]
    public async Task Cancelar_SiNoSePuedeLiberarElStock_NoCancelaNada()
    {
        var user = Guid.NewGuid();
        var order = GivenOrder(user, paid: false);
        _inventory.ReleaseReservationAsync(default, default!, default).ReturnsForAnyArgs<Task>(_ => throw new HttpRequestException("Inventario caído"));

        var act = () => Canceller().Handle(new CancelOrderCommand(order.Id, user, false, "ChangedMind", null, "t"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        order.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public async Task Cancelar_PagadoComoCliente_SoloPideLaCancelacion()
    {
        var user = Guid.NewGuid();
        var order = GivenOrder(user, paid: true);

        var result = await Canceller().Handle(new CancelOrderCommand(order.Id, user, false, "ChangedMind", "Ya no lo necesito", "t"), CancellationToken.None);

        result.Status.Should().Be("Paid");
        result.Returns!.Single().Should().Match<ReturnResult>(r => r.IsCancellation && r.Status == "Requested");
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(ApproveReturnCommand)!, default);
    }

    [Fact]
    public async Task Cancelar_PagadoComoAdmin_LaApruebaEnElActo_ReusandoLaQuePidioElCliente()
    {
        var order = GivenOrder(Guid.NewGuid(), paid: true);
        var asked = order.RequestCancellation(ReturnReason.ChangedMind, null, DateTime.UtcNow);

        await Canceller().Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "Other", "Sin stock real", "admin"), CancellationToken.None);

        await _mediator.Received(1).Send(Arg.Is<ApproveReturnCommand>(c => c.ReturnId == asked.Id && c.AccessToken == "admin"), Arg.Any<CancellationToken>());
        order.Returns.Should().ContainSingle("no se crea una segunda solicitud");
    }

    [Fact]
    public async Task Cancelar_ElPedidoDeOtroCliente_EsNotFound()
    {
        var order = GivenOrder(Guid.NewGuid(), paid: false);

        var act = () => Canceller().Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), false, "ChangedMind", null, "t"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
        await _inventory.DidNotReceiveWithAnyArgs().ReleaseReservationAsync(default, default!, default);
    }

    [Fact]
    public async Task Cancelar_SinPagarPeroPayPalYaCobro_NoCancelaNiLiberaNada()
    {
        var user = Guid.NewGuid();
        var order = GivenOrder(user, paid: false);
        _payments.GetPaymentStatusAsync(order.Id, "t", Arg.Any<CancellationToken>()).Returns("Captured");

        var act = () => Canceller().Handle(new CancelOrderCommand(order.Id, user, false, "ChangedMind", null, "t"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*ya se cobró*");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        await _inventory.DidNotReceiveWithAnyArgs().ReleaseReservationAsync(default, default!, default);
    }

    [Fact]
    public async Task Cancelar_SinPagarComoAdmin_NoSePuede()
    {
        var order = GivenOrder(Guid.NewGuid(), paid: false);

        var act = () => Canceller().Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "Other", null, "admin"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
        order.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public async Task Cancelar_ComoAdmin_SuComentarioVaComoNotaYNoComoDelCliente()
    {
        var order = GivenOrder(Guid.NewGuid(), paid: true);

        await Canceller().Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "Other", "Sin stock", "admin"), CancellationToken.None);

        order.Returns.Single().Comment.Should().BeNull();
        await _mediator.Received(1).Send(Arg.Is<ApproveReturnCommand>(c => c.Note == "Sin stock"), Arg.Any<CancellationToken>());
    }
}

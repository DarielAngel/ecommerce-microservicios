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

    private Order GivenShippedOrder(Guid? userId = null)
    {
        var order = Order.Create(Guid.NewGuid(), userId ?? Guid.NewGuid(), "ana@test.com", "Ana", "Calle 1",
            new[] { (_mug, Guid.NewGuid(), "Taza", "TZ", 20m, 2) });
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
}

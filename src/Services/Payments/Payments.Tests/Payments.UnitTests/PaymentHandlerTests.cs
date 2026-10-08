using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Application.Features;
using Ecommerce.Payments.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Payments.UnitTests;

public class CreatePaymentCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IPayPalClient _payPalClient = Substitute.For<IPayPalClient>();

    private CreatePaymentCommandHandler CreateHandler() => new(_paymentRepository, _payPalClient);

    [Fact]
    public async Task Handle_SinPagoPrevio_DeberiaCrearOrdenEnPayPalYGuardarElPago()
    {
        var orderId = Guid.NewGuid();
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _payPalClient.CreateOrderAsync(100m, "USD", orderId, Arg.Any<CancellationToken>())
            .Returns(new CreatePayPalOrderResult("PP-123", "https://paypal.com/approve/PP-123"));

        var handler = CreateHandler();
        var result = await handler.Handle(new CreatePaymentCommand(orderId, 100m, "USD"), CancellationToken.None);

        result.ApproveUrl.Should().Be("https://paypal.com/approve/PP-123");
        result.Status.Should().Be("PendingApproval");
        await _paymentRepository.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConPagoYaExistenteParaLaOrden_DeberiaSerIdempotenteYNoLlamarAPayPalDeNuevo()
    {
        var orderId = Guid.NewGuid();
        var existingPayment = Payment.Create(orderId, 100m, "USD", "PP-YA-EXISTE");
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(existingPayment);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreatePaymentCommand(orderId, 100m, "USD"), CancellationToken.None);

        result.PaymentId.Should().Be(existingPayment.Id);
        await _payPalClient.DidNotReceive().CreateOrderAsync(
            Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }
}

public class CapturePaymentCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IPayPalClient _payPalClient = Substitute.For<IPayPalClient>();

    private CapturePaymentCommandHandler CreateHandler() => new(_paymentRepository, _payPalClient);

    [Fact]
    public async Task Handle_ConCapturaExitosa_DeberiaMarcarElPagoComoCaptured()
    {
        var orderId = Guid.NewGuid();
        var payment = Payment.Create(orderId, 100m, "USD", "PP-123");
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        _payPalClient.CaptureOrderAsync("PP-123", Arg.Any<CancellationToken>())
            .Returns(new CapturePayPalOrderResult(true, "CAPTURE-1", null));

        var handler = CreateHandler();
        var result = await handler.Handle(new CapturePaymentCommand(orderId), CancellationToken.None);

        result.Status.Should().Be("Captured");
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConCapturaFallida_DeberiaMarcarFallidoYLanzarConflictAppException()
    {
        var orderId = Guid.NewGuid();
        var payment = Payment.Create(orderId, 100m, "USD", "PP-123");
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        _payPalClient.CaptureOrderAsync("PP-123", Arg.Any<CancellationToken>())
            .Returns(new CapturePayPalOrderResult(false, null, "Tarjeta rechazada"));

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new CapturePaymentCommand(orderId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
    }

    [Fact]
    public async Task Handle_SinPagoParaLaOrden_DeberiaLanzarNotFoundAppException()
    {
        _paymentRepository.GetByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Payment?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new CapturePaymentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Handle_SobreUnPagoYaCapturado_DeberiaSerIdempotenteYNoLlamarAPayPalDeNuevo()
    {
        var orderId = Guid.NewGuid();
        var payment = Payment.Create(orderId, 100m, "USD", "PP-123");
        payment.MarkCaptured("CAPTURE-1");
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(new CapturePaymentCommand(orderId), CancellationToken.None);

        result.Status.Should().Be("Captured");
        await _payPalClient.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public class RefundPaymentCommandHandlerTests
{
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IPayPalClient _payPal = Substitute.For<IPayPalClient>();

    private RefundPaymentCommandHandler CreateHandler() => new(
        _payments, _payPal, TimeProvider.System,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<RefundPaymentCommandHandler>.Instance);

    private Payment GivenCapturedPayment(Guid orderId, decimal amount = 100m)
    {
        var payment = Payment.Create(orderId, amount, "USD", "PP-123");
        payment.MarkCaptured("CAPTURE-1");
        _payments.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        return payment;
    }

    [Fact]
    public async Task Handle_ReembolsaEnPayPalConLaClaveDeLaDevolucionYLoRegistra()
    {
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        GivenCapturedPayment(orderId);
        _payPal.RefundCaptureAsync("CAPTURE-1", 25.5m, "USD", refundId.ToString(), "Llegó roto", Arg.Any<CancellationToken>())
            .Returns(new RefundPayPalCaptureResult(true, "PP-REFUND-1", "COMPLETED", null));

        var result = await CreateHandler().Handle(new RefundPaymentCommand(orderId, refundId, 25.5m, "Llegó roto"), CancellationToken.None);

        result.Should().Match<RefundResult>(r =>
            r.RefundId == refundId && r.PayPalRefundId == "PP-REFUND-1" && r.TotalRefunded == 25.5m && r.Refundable == 74.5m
            && r.PaymentStatus == "Captured");
        await _payments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ElMismoReembolsoDosVeces_NoVuelveALlamarAPayPal()
    {
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var payment = GivenCapturedPayment(orderId);
        payment.AddRefund(refundId, 40m, "PP-REFUND-1", null, DateTime.UtcNow);

        var result = await CreateHandler().Handle(new RefundPaymentCommand(orderId, refundId, 40m, null), CancellationToken.None);

        result.PayPalRefundId.Should().Be("PP-REFUND-1");
        await _payPal.DidNotReceiveWithAnyArgs().RefundCaptureAsync(default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_ElMismoIdConOtroMonto_EsUnConflicto()
    {
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        GivenCapturedPayment(orderId).AddRefund(refundId, 40m, "PP-REFUND-1", null, DateTime.UtcNow);

        var act = () => CreateHandler().Handle(new RefundPaymentCommand(orderId, refundId, 41m, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
    }

    [Fact]
    public async Task Handle_MasDeLoQueQueda_EsConflictoSinLlamarAPayPal()
    {
        var orderId = Guid.NewGuid();
        GivenCapturedPayment(orderId, 50m);

        var act = () => CreateHandler().Handle(new RefundPaymentCommand(orderId, Guid.NewGuid(), 50.01m, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*quedan 50.00 USD*");
        await _payPal.DidNotReceiveWithAnyArgs().RefundCaptureAsync(default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_SiPayPalRechaza_NoRegistraNadaYAvisa()
    {
        var orderId = Guid.NewGuid();
        var payment = GivenCapturedPayment(orderId);
        _payPal.RefundCaptureAsync(default!, default, default!, default!, default, default)
            .ReturnsForAnyArgs(new RefundPayPalCaptureResult(false, null, null, "PayPal rechazó el reembolso (422)."));

        var act = () => CreateHandler().Handle(new RefundPaymentCommand(orderId, Guid.NewGuid(), 10m, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*422*");
        payment.Refunds.Should().BeEmpty();
        await _payments.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PagoSinCapturar_EsConflicto()
    {
        var orderId = Guid.NewGuid();
        _payments.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(Payment.Create(orderId, 10m, "USD", "PP-9"));

        var act = () => CreateHandler().Handle(new RefundPaymentCommand(orderId, Guid.NewGuid(), 5m, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*capturado*");
    }
}

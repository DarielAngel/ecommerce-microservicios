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

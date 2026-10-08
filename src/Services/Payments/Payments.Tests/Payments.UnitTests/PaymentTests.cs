using Ecommerce.Payments.Domain.Entities;
using Ecommerce.Payments.Domain.Enums;
using Ecommerce.Payments.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Payments.UnitTests;

public class PaymentTests
{
    private static Payment CreateValidPayment() =>
        Payment.Create(Guid.NewGuid(), 100m, "USD", "PAYPAL-ORDER-123");

    [Fact]
    public void Create_ConDatosValidos_DeberiaQuedarPendingApproval()
    {
        var payment = CreateValidPayment();

        payment.Status.Should().Be(PaymentStatus.PendingApproval);
        payment.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_ConMontoInvalido_DeberiaLanzarDomainException(decimal amount)
    {
        var act = () => Payment.Create(Guid.NewGuid(), amount, "USD", "PAYPAL-ORDER-123");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void Create_ConMonedaInvalida_DeberiaLanzarDomainException(string currency)
    {
        var act = () => Payment.Create(Guid.NewGuid(), 100m, currency, "PAYPAL-ORDER-123");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkCaptured_DesdePendingApproval_DeberiaQuedarCaptured()
    {
        var payment = CreateValidPayment();

        payment.MarkCaptured("CAPTURE-1");

        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.PayPalCaptureId.Should().Be("CAPTURE-1");
        payment.CapturedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCaptured_DosVecesConElMismoCaptureId_DeberiaSerIdempotente()
    {
        var payment = CreateValidPayment();
        payment.MarkCaptured("CAPTURE-1");

        var act = () => payment.MarkCaptured("CAPTURE-1");

        act.Should().NotThrow("un webhook duplicado con el mismo captureId no debería fallar");
        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public void MarkCaptured_DosVecesConCaptureIdDistinto_DeberiaLanzarDomainException()
    {
        var payment = CreateValidPayment();
        payment.MarkCaptured("CAPTURE-1");

        var act = () => payment.MarkCaptured("CAPTURE-2");

        act.Should().Throw<DomainException>("un captureId distinto en un pago ya capturado es una anomalía real, no debe silenciarse");
    }

    [Fact]
    public void MarkCaptured_SobreUnPagoFallido_DeberiaLanzarDomainException()
    {
        var payment = CreateValidPayment();
        payment.MarkFailed("razón de prueba");

        var act = () => payment.MarkCaptured("CAPTURE-1");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkFailed_SobreUnPagoYaCapturado_DeberiaLanzarDomainException()
    {
        var payment = CreateValidPayment();
        payment.MarkCaptured("CAPTURE-1");

        var act = () => payment.MarkFailed("intento de fallar algo ya capturado");

        act.Should().Throw<DomainException>();
    }

    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private static Payment CapturedPayment()
    {
        var payment = CreateValidPayment();
        payment.MarkCaptured("CAPTURE-1");
        return payment;
    }

    [Fact]
    public void AddRefund_Parciales_SumanYAlDevolverTodoQuedaRefunded()
    {
        var payment = CapturedPayment();

        payment.AddRefund(Guid.NewGuid(), 30m, "R-1", "talla", Now);
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.RefundedAmount.Should().Be(30m);
        payment.RefundableAmount.Should().Be(70m);

        payment.AddRefund(Guid.NewGuid(), 70m, "R-2", null, Now);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundableAmount.Should().Be(0m);
        payment.Refunds.Should().HaveCount(2);
    }

    [Fact]
    public void AddRefund_SinHaberSidoCapturado_DeberiaLanzarDomainException()
    {
        var act = () => CreateValidPayment().AddRefund(Guid.NewGuid(), 10m, "R-1", null, Now);

        act.Should().Throw<DomainException>().WithMessage("*capturado*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    [InlineData(100.01)]
    public void EnsureCanRefund_MontosInvalidosOMayoresAlSaldo_Fallan(decimal amount)
    {
        var act = () => CapturedPayment().EnsureCanRefund(amount);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddRefund_NoPermiteDevolverMasDeLoQueQueda()
    {
        var payment = CapturedPayment();
        payment.AddRefund(Guid.NewGuid(), 80m, "R-1", null, Now);

        var act = () => payment.AddRefund(Guid.NewGuid(), 20.01m, "R-2", null, Now);

        act.Should().Throw<DomainException>().WithMessage("*quedan 20.00 USD*");
    }

    [Fact]
    public void AddRefund_ConElMismoId_DeberiaLanzarDomainException()
    {
        var payment = CapturedPayment();
        var id = Guid.NewGuid();
        payment.AddRefund(id, 10m, "R-1", null, Now);

        var act = () => payment.AddRefund(id, 10m, "R-1", null, Now);

        act.Should().Throw<DomainException>();
        payment.RefundedAmount.Should().Be(10m);
    }
}

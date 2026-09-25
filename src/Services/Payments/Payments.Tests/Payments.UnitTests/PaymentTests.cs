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

    [Fact]
    public void MarkRefunded_SoloDesdeCaptured_DeberiaFuncionar()
    {
        var payment = CreateValidPayment();
        payment.MarkCaptured("CAPTURE-1");

        payment.MarkRefunded();

        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void MarkRefunded_SinHaberSidoCapturado_DeberiaLanzarDomainException()
    {
        var payment = CreateValidPayment();

        var act = () => payment.MarkRefunded();

        act.Should().Throw<DomainException>();
    }
}

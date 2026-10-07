using Ecommerce.Promotions.Domain.Entities;
using Ecommerce.Promotions.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Promotions.UnitTests;

public class CouponTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    // ---- Creación ----

    [Theory]
    [InlineData(" verano25 ", "VERANO25")]
    [InlineData("black-friday_2026", "BLACK-FRIDAY_2026")]
    public void Create_DeberiaNormalizarElCodigoAMayusculasSinEspacios(string input, string expected)
    {
        Coupons.Percentage(code: input).Code.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("CON ESPACIO")]
    [InlineData("ÑANDÚ10")]
    [InlineData("-EMPIEZAGUION")]
    [InlineData("UNCODIGOEXAGERADAMENTELARGO12345")]
    public void Create_ConCodigoInvalido_DeberiaRechazarlo(string code)
    {
        var act = () => Coupons.Percentage(code: code);
        act.Should().Throw<DomainException>().WithMessage("*código*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(90.01)]
    [InlineData(100)]
    public void Create_PorcentajeFueraDeRango_DeberiaRechazarlo(decimal value)
    {
        var act = () => Coupons.Percentage(value: value);
        act.Should().Throw<DomainException>().WithMessage("*porcentaje*");
    }

    [Fact]
    public void Create_TopeEnUnCuponDeMontoFijo_DeberiaRechazarlo()
    {
        var act = () => Coupon.Create("FIJO5", "x", DiscountType.FixedAmount, 5, maxDiscountAmount: 3, 0, null, null, null, false);
        act.Should().Throw<DomainException>().WithMessage("*solo aplica a cupones de porcentaje*");
    }

    [Fact]
    public void Create_ConFinAntesDelInicio_DeberiaRechazarlo()
    {
        var act = () => Coupons.Percentage(starts: Now, ends: Now.AddDays(-1));
        act.Should().Throw<DomainException>().WithMessage("*fecha de fin*");
    }

    [Fact]
    public void Create_ConLimiteDeUsosCero_DeberiaRechazarlo()
    {
        var act = () => Coupons.Percentage(limit: 0);
        act.Should().Throw<DomainException>().WithMessage("*límite de usos*");
    }

    [Fact]
    public void Create_SinDescripcion_DeberiaRechazarlo()
    {
        var act = () => Coupon.Create("SINDESC", "  ", DiscountType.Percentage, 10, null, 0, null, null, null, false);
        act.Should().Throw<DomainException>().WithMessage("*descripción*");
    }

    [Fact]
    public void Update_ConDatosInvalidos_NoDeberiaCambiarNada()
    {
        var coupon = Coupons.Percentage(value: 10);

        var act = () => coupon.Update("Nueva", DiscountType.Percentage, 500, null, 0, null, null, null, false, true);

        act.Should().Throw<DomainException>();
        coupon.Value.Should().Be(10);
        coupon.Description.Should().Be("Descuento de verano");
    }

    // ---- Cálculo del descuento ----

    [Fact]
    public void Porcentaje_DeberiaRedondearADosDecimales()
    {
        // 15 % de 33.33 = 4.9995 → 5.00
        Coupons.Percentage(value: 15).CalculateDiscount(33.33m, Now).Should().Be(5.00m);
    }

    [Fact]
    public void Porcentaje_ConTope_NoDeberiaSuperarElTope()
    {
        var coupon = Coupons.Percentage(value: 20, max: 50);

        coupon.CalculateDiscount(100m, Now).Should().Be(20m);
        coupon.CalculateDiscount(1000m, Now).Should().Be(50m);
    }

    [Fact]
    public void MontoFijo_DeberiaDescontarElMonto()
    {
        Coupons.Fixed(value: 10).CalculateDiscount(45.50m, Now).Should().Be(10m);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(8)]
    public void MontoFijo_QueIgualaOSuperaLaCompra_DeberiaRechazarse(decimal subtotal)
    {
        // Dejaría la orden en $0 (o negativa): PayPal no cobra $0.
        var act = () => Coupons.Fixed(value: 10).CalculateDiscount(subtotal, Now);
        act.Should().Throw<CouponNotApplicableException>().WithMessage("*tu compra tiene que superar*");
    }

    [Fact]
    public void BajoLaCompraMinima_DeberiaRechazarseConElMontoQueFalta()
    {
        var act = () => Coupons.Percentage(minimum: 50).CalculateDiscount(49.99m, Now);
        act.Should().Throw<CouponNotApplicableException>().WithMessage("*compra mínima de $50.00*");
    }

    [Fact]
    public void JustoEnLaCompraMinima_DeberiaAplicar()
    {
        Coupons.Percentage(value: 10, minimum: 50).CalculateDiscount(50m, Now).Should().Be(5m);
    }

    [Fact]
    public void AntesDeEmpezar_DeberiaRechazarse()
    {
        var act = () => Coupons.Percentage(starts: Now.AddDays(1)).CalculateDiscount(100, Now);
        act.Should().Throw<CouponNotApplicableException>().WithMessage("*todavía no está vigente*");
    }

    [Fact]
    public void JustoAlVencer_DeberiaRechazarse()
    {
        var act = () => Coupons.Percentage(ends: Now).CalculateDiscount(100, Now);
        act.Should().Throw<CouponNotApplicableException>().WithMessage("*venció*");
    }

    [Fact]
    public void DentroDeLaVigencia_DeberiaAplicar()
    {
        Coupons.Percentage(value: 10, starts: Now.AddDays(-1), ends: Now.AddDays(1))
            .CalculateDiscount(100, Now).Should().Be(10);
    }

    [Fact]
    public void Inactivo_DeberiaRechazarse()
    {
        var coupon = Coupons.Percentage();
        coupon.Update(coupon.Description, coupon.Type, coupon.Value, null, 0, null, null, null, false, isActive: false);

        var act = () => coupon.CalculateDiscount(100, Now);
        act.Should().Throw<CouponNotApplicableException>().WithMessage("*ya no está disponible*");
    }
}

public class CouponRedemptionTests
{
    private static CouponRedemption NewRedemption() =>
        CouponRedemption.Reserve(Guid.NewGuid(), Coupons.Percentage(), Guid.NewGuid(), 100, 10);

    [Fact]
    public void Reserve_DeberiaQuedarReservadoConElCodigoDelCupon()
    {
        var r = NewRedemption();
        r.Status.Should().Be(RedemptionStatus.Reserved);
        r.Code.Should().Be("VERANO10");
        r.DiscountAmount.Should().Be(10);
    }

    [Fact]
    public void Confirm_YLuegoConfirmarOtraVez_DeberiaSerIdempotente()
    {
        var r = NewRedemption();
        r.Confirm();
        r.Confirm();
        r.Status.Should().Be(RedemptionStatus.Confirmed);
    }

    [Fact]
    public void Release_YLuegoLiberarOtraVez_DeberiaSerIdempotente()
    {
        var r = NewRedemption();
        r.Release();
        r.Release();
        r.Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public void Release_DeUnUsoYaConfirmado_DeberiaRechazarse()
    {
        var r = NewRedemption();
        r.Confirm();
        var act = r.Release;
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_DeUnUsoYaLiberado_DeberiaRechazarse()
    {
        var r = NewRedemption();
        r.Release();
        var act = r.Confirm;
        act.Should().Throw<DomainException>();
    }
}

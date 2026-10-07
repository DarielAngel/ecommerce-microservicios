using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Application.Features;
using Ecommerce.Promotions.Domain.Entities;
using Ecommerce.Promotions.Domain.Exceptions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Promotions.UnitTests;

public abstract class HandlerTestBase
{
    protected readonly ICouponRepository CouponsRepo = Substitute.For<ICouponRepository>();
    protected readonly IRedemptionRepository Redemptions = Substitute.For<IRedemptionRepository>();
    protected readonly IUnitOfWork UnitOfWork = Substitute.For<IUnitOfWork>();
    protected readonly FixedTimeProvider Time = new();
    protected readonly Guid UserId = Guid.NewGuid();

    protected HandlerTestBase()
    {
        // La "transacción" del doble simplemente ejecuta el trabajo.
        UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<RedemptionResult>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task<RedemptionResult>>>()());
        Redemptions.GetUsageAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new CouponUsage(0, 0));
    }

    protected void GivenCoupon(Coupon coupon)
    {
        CouponsRepo.GetByCodeAsync(coupon.Code, Arg.Any<CancellationToken>()).Returns(coupon);
        CouponsRepo.GetByCodeForUpdateAsync(coupon.Code, Arg.Any<CancellationToken>()).Returns(coupon);
        CouponsRepo.GetByIdAsync(coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);
    }

    protected void GivenUsage(Coupon coupon, int confirmed, int reserved) =>
        Redemptions.GetUsageAsync(coupon.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new CouponUsage(confirmed, reserved));
}

public class ValidateCouponQueryHandlerTests : HandlerTestBase
{
    private Task<CouponQuoteResult> Validate(string code, decimal subtotal) =>
        new ValidateCouponQueryHandler(CouponsRepo, Redemptions, Time)
            .Handle(new ValidateCouponQuery(code, UserId, subtotal), CancellationToken.None);

    [Fact]
    public async Task ConCuponValido_DeberiaDevolverDescuentoYTotal_SinImportarMayusculas()
    {
        GivenCoupon(Coupons.Percentage(value: 15));

        var quote = await Validate("  verano10 ", 80m);

        quote.Code.Should().Be("VERANO10");
        quote.DiscountAmount.Should().Be(12m);
        quote.Total.Should().Be(68m);
        quote.Description.Should().Be("Descuento de verano");
    }

    [Fact]
    public async Task ConCodigoInexistente_DeberiaDecirloConElCodigo()
    {
        var act = () => Validate("NOEXISTE", 50);
        await act.Should().ThrowAsync<NotFoundAppException>().WithMessage("*NOEXISTE*no existe*");
    }

    [Fact]
    public async Task ConLimiteAlcanzado_ContandoReservasVigentes_DeberiaRechazarse()
    {
        var coupon = Coupons.Percentage(limit: 5);
        GivenCoupon(coupon);
        GivenUsage(coupon, confirmed: 3, reserved: 2);

        var act = () => Validate(coupon.Code, 50);
        await act.Should().ThrowAsync<CouponNotApplicableException>().WithMessage("*límite de usos*");
    }

    [Fact]
    public async Task ConUnUsoDisponible_DeberiaAplicar()
    {
        var coupon = Coupons.Percentage(limit: 5);
        GivenCoupon(coupon);
        GivenUsage(coupon, confirmed: 3, reserved: 1);

        (await Validate(coupon.Code, 50)).DiscountAmount.Should().Be(5);
    }

    [Fact]
    public async Task DeUnSoloUsoPorCliente_SiYaLoUso_DeberiaRechazarse()
    {
        var coupon = Coupons.Percentage(oncePerCustomer: true);
        GivenCoupon(coupon);
        Redemptions.HasActiveForUserAsync(coupon.Id, UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => Validate(coupon.Code, 50);
        await act.Should().ThrowAsync<CouponNotApplicableException>().WithMessage("*un solo uso por cliente*");
    }

    [Fact]
    public async Task DeberiaUsarLaHoraDelRelojInyectado()
    {
        GivenCoupon(Coupons.Percentage(ends: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

        var act = () => Validate("VERANO10", 50); // el reloj fijo dice 15/10/2026
        await act.Should().ThrowAsync<CouponNotApplicableException>().WithMessage("*venció*");
    }
}

public class ReserveRedemptionCommandHandlerTests : HandlerTestBase
{
    private readonly Guid _orderId = Guid.NewGuid();

    private Task<RedemptionResult> Reserve(string code = "VERANO10", decimal subtotal = 100, Guid? userId = null) =>
        new ReserveRedemptionCommandHandler(CouponsRepo, Redemptions, UnitOfWork, Time)
            .Handle(new ReserveRedemptionCommand(_orderId, code, userId ?? UserId, subtotal), CancellationToken.None);

    [Fact]
    public async Task ConCuponValido_DeberiaReservarConElDescuentoYGuardarDentroDeUnaTransaccion()
    {
        GivenCoupon(Coupons.Percentage(value: 10));

        var result = await Reserve();

        result.Status.Should().Be("Reserved");
        result.DiscountAmount.Should().Be(10);
        await Redemptions.Received(1).AddAsync(
            Arg.Is<CouponRedemption>(r => r.OrderId == _orderId && r.UserId == UserId && r.DiscountAmount == 10),
            Arg.Any<CancellationToken>());
        await UnitOfWork.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task<RedemptionResult>>>(), Arg.Any<CancellationToken>());
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeberiaLeerElCuponConBloqueo_NoConLaLecturaNormal()
    {
        GivenCoupon(Coupons.Percentage());

        await Reserve();

        await CouponsRepo.Received(1).GetByCodeForUpdateAsync("VERANO10", Arg.Any<CancellationToken>());
        await CouponsRepo.DidNotReceive().GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReintentoDeLaMismaOrden_DeberiaDevolverElCanjeExistenteSinContarOtroUso()
    {
        var coupon = Coupons.Percentage();
        var existing = CouponRedemption.Reserve(_orderId, coupon, UserId, 100, 10);
        Redemptions.GetByOrderIdAsync(_orderId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Reserve();

        result.OrderId.Should().Be(_orderId);
        await Redemptions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task OrdenDeOtroCliente_DeberiaRechazarse()
    {
        var existing = CouponRedemption.Reserve(_orderId, Coupons.Percentage(), Guid.NewGuid(), 100, 10);
        Redemptions.GetByOrderIdAsync(_orderId, Arg.Any<CancellationToken>()).Returns(existing);

        var act = () => Reserve();
        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task CuponAgotado_NoDeberiaReservar()
    {
        var coupon = Coupons.Percentage(limit: 1);
        GivenCoupon(coupon);
        GivenUsage(coupon, confirmed: 1, reserved: 0);

        var act = () => Reserve();

        await act.Should().ThrowAsync<CouponNotApplicableException>();
        await Redemptions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CuponInexistente_DeberiaLanzarNotFound()
    {
        var act = () => Reserve("NADA");
        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

public class ConfirmAndReleaseHandlerTests : HandlerTestBase
{
    private readonly Guid _orderId = Guid.NewGuid();

    private CouponRedemption GivenReserved()
    {
        var r = CouponRedemption.Reserve(_orderId, Coupons.Percentage(), UserId, 100, 10);
        Redemptions.GetByOrderIdAsync(_orderId, Arg.Any<CancellationToken>()).Returns(r);
        return r;
    }

    [Fact]
    public async Task Confirm_DeberiaDejarElUsoComoDefinitivo()
    {
        var r = GivenReserved();

        var result = await new ConfirmRedemptionCommandHandler(Redemptions, UnitOfWork)
            .Handle(new ConfirmRedemptionCommand(_orderId, UserId), CancellationToken.None);

        result.Status.Should().Be("Confirmed");
        r.Status.Should().Be(RedemptionStatus.Confirmed);
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_SinCanje_DeberiaLanzarNotFound()
    {
        var act = () => new ConfirmRedemptionCommandHandler(Redemptions, UnitOfWork)
            .Handle(new ConfirmRedemptionCommand(Guid.NewGuid(), UserId), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Confirm_DeOtroCliente_DeberiaRechazarse()
    {
        GivenReserved();
        var act = () => new ConfirmRedemptionCommandHandler(Redemptions, UnitOfWork)
            .Handle(new ConfirmRedemptionCommand(_orderId, Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task Release_DeberiaLiberarElUso()
    {
        var r = GivenReserved();

        await new ReleaseRedemptionCommandHandler(Redemptions, UnitOfWork)
            .Handle(new ReleaseRedemptionCommand(_orderId, UserId), CancellationToken.None);

        r.Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public async Task Release_DeUnaOrdenSinCupon_NoDeberiaHacerNadaNiFallar()
    {
        // Órdenes llama a la compensación sin preguntar si la orden tenía cupón.
        await new ReleaseRedemptionCommandHandler(Redemptions, UnitOfWork)
            .Handle(new ReleaseRedemptionCommand(Guid.NewGuid(), UserId), CancellationToken.None);

        await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class AdminCouponHandlerTests : HandlerTestBase
{
    private static CouponInput Input(decimal value = 10, bool isActive = true) =>
        new("Promo", DiscountType.Percentage, value, null, 0, null, null, null, false, isActive);

    [Fact]
    public async Task Create_DeberiaGuardarElCuponConElCodigoNormalizado()
    {
        var result = await new CreateCouponCommandHandler(CouponsRepo, UnitOfWork)
            .Handle(new CreateCouponCommand("bienvenida", Input()), CancellationToken.None);

        result.Code.Should().Be("BIENVENIDA");
        result.TimesUsed.Should().Be(0);
        await CouponsRepo.Received(1).AddAsync(Arg.Is<Coupon>(c => c.Code == "BIENVENIDA"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InactivoDesdeElInicio_DeberiaQuedarInactivo()
    {
        var result = await new CreateCouponCommandHandler(CouponsRepo, UnitOfWork)
            .Handle(new CreateCouponCommand("PAUSADO", Input(isActive: false)), CancellationToken.None);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ConCodigoRepetido_DeberiaLanzarConflicto()
    {
        CouponsRepo.CodeExistsAsync("BIENVENIDA", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new CreateCouponCommandHandler(CouponsRepo, UnitOfWork)
            .Handle(new CreateCouponCommand("Bienvenida", Input()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
        await CouponsRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Update_DeberiaCambiarLosDatosYDevolverLosUsos()
    {
        var coupon = Coupons.Percentage(value: 10);
        GivenCoupon(coupon);
        GivenUsage(coupon, confirmed: 4, reserved: 1);

        var result = await new UpdateCouponCommandHandler(CouponsRepo, Redemptions, UnitOfWork, Time)
            .Handle(new UpdateCouponCommand(coupon.Id, Input(value: 25, isActive: false)), CancellationToken.None);

        result.Value.Should().Be(25);
        result.IsActive.Should().BeFalse();
        result.TimesUsed.Should().Be(4);
        result.ActiveReservations.Should().Be(1);
    }

    [Fact]
    public async Task Update_DeUnCuponInexistente_DeberiaLanzarNotFound()
    {
        var act = () => new UpdateCouponCommandHandler(CouponsRepo, Redemptions, UnitOfWork, Time)
            .Handle(new UpdateCouponCommand(Guid.NewGuid(), Input()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task List_DeberiaIncluirLosUsosDeCadaCupon_YCeroParaLosNoUsados()
    {
        var used = Coupons.Percentage(code: "USADO");
        var unused = Coupons.Fixed(code: "NUEVO");
        CouponsRepo.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Coupon> { used, unused });
        Redemptions.GetUsageAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CouponUsage> { [used.Id] = new(7, 2) });

        var result = await new ListCouponsQueryHandler(CouponsRepo, Redemptions, Time).Handle(new ListCouponsQuery(), CancellationToken.None);

        result.Single(c => c.Code == "USADO").TimesUsed.Should().Be(7);
        result.Single(c => c.Code == "NUEVO").TimesUsed.Should().Be(0);
        result.Single(c => c.Code == "NUEVO").Type.Should().Be("FixedAmount");
    }
}

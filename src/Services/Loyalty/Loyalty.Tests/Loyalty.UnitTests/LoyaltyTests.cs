using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Application.Features;
using Ecommerce.Loyalty.Domain.Entities;
using Ecommerce.Loyalty.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ecommerce.Loyalty.UnitTests;

/// <summary>Reloj fijo para las pruebas.</summary>
internal sealed class FixedTime : TimeProvider
{
    public DateTime Now { get; set; } = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(Now);
}

/// <summary>Repositorio en memoria con la misma regla de unicidad que la base (orden + tipo).</summary>
internal sealed class InMemoryLoyalty : ILoyaltyRepository, IUnitOfWork
{
    public List<LoyaltyEntry> Saved { get; } = new();
    private readonly List<LoyaltyEntry> _pending = new();
    public int Locks { get; private set; }

    public Task LockAccountAsync(Guid userId, CancellationToken ct) { Locks++; return Task.CompletedTask; }

    public Task<IReadOnlyList<LoyaltyEntry>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoyaltyEntry>>(Saved.Where(e => e.UserId == userId).ToList());

    public Task<LoyaltyEntry?> GetAsync(Guid orderId, LoyaltyEntryKind kind, CancellationToken ct) =>
        Task.FromResult(Saved.FirstOrDefault(e => e.OrderId == orderId && e.Kind == kind));

    public Task AddAsync(LoyaltyEntry entry, CancellationToken ct) { _pending.Add(entry); return Task.CompletedTask; }

    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => work();

    public Task SaveChangesAsync(CancellationToken ct)
    {
        foreach (var e in _pending)
        {
            if (Saved.Any(s => s.OrderId == e.OrderId && s.Kind == e.Kind))
            {
                _pending.Clear();
                throw new ConflictAppException("duplicado");
            }
            Saved.Add(e);
        }
        _pending.Clear();
        return Task.CompletedTask;
    }
}

public class LoyaltyRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.99, 0)]
    [InlineData(25.50, 25)]
    [InlineData(799, 799)]
    public void SeGanaUnPuntoPorDolarPagado_Redondeando_HaciaAbajo(decimal paid, int points)
    {
        LoyaltyRules.PointsEarnedFor(paid).Should().Be(points);
    }

    [Fact]
    public void Quote_UsaTodoElSaldoSiNoPasaElTopeDeLaMitad()
    {
        // 250 puntos = $2.50; la compra es de $40 → tope $20: alcanza para todo.
        LoyaltyRules.Quote(250, 40m).Should().Be(new RedemptionQuote(250, 2.50m));
    }

    [Fact]
    public void Quote_NoPasaDeLaMitadDeLaCompra()
    {
        // 5000 puntos = $50, pero la compra es de $30 → como mucho $15 (1500 puntos).
        LoyaltyRules.Quote(5000, 30m).Should().Be(new RedemptionQuote(1500, 15m));
    }

    [Theory]
    [InlineData(99, 100)]   // no llega al mínimo
    [InlineData(500, 1.5)]  // la mitad de $1.50 son 75 puntos: menos que el mínimo
    [InlineData(500, 0)]
    public void Quote_SinMinimo_NoSeOfrece(int balance, decimal amount)
    {
        LoyaltyRules.Quote(balance, amount).Should().Be(RedemptionQuote.None);
    }
}

public class PointsLedgerTests
{
    private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void Saldo_GanadoMenosCanjeado_YLosCanjesDeshechosOVencidosNoCuentan()
    {
        var earned = LoyaltyEntry.Earn(User, Guid.NewGuid(), 600m, T0)!;          // +600
        var confirmed = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(200, 2m), T0);
        confirmed.Confirm(T0);                                                       // −200
        var released = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(100, 1m), T0);
        released.Release(T0);                                                        // no cuenta
        var stale = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(150, 1.5m), T0); // vence a las 2 h
        var fresh = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(100, 1m), T0.AddHours(2)); // −100

        var all = new[] { earned, confirmed, released, stale, fresh };

        PointsLedger.Balance(all, T0.AddMinutes(1)).Should().Be(600 - 200 - 150 - 100);
        // Dos horas después, la reserva vieja ya venció; la nueva sigue contando.
        PointsLedger.Balance(all, T0.AddHours(2).AddMinutes(1)).Should().Be(600 - 200 - 100);
    }

    [Fact]
    public void UnCanjeConfirmadoNoSePuedeDeshacer_YUnoDeshechoNoSeConfirma()
    {
        var a = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(100, 1m), T0);
        a.Confirm(T0);
        a.Confirm(T0); // idempotente
        a.Invoking(x => x.Release(T0)).Should().Throw<DomainException>();

        var b = LoyaltyEntry.Reserve(User, Guid.NewGuid(), new(100, 1m), T0);
        b.Release(T0);
        b.Release(T0); // idempotente
        b.Invoking(x => x.Confirm(T0)).Should().Throw<DomainException>();
    }
}

public class LoyaltyHandlerTests
{
    private readonly InMemoryLoyalty _db = new();
    private readonly FixedTime _time = new();
    private readonly Guid _user = Guid.NewGuid();

    private Task Earn(decimal amount, Guid? orderId = null) =>
        new EarnPointsCommandHandler(_db, _db, _time, NullLogger<EarnPointsCommandHandler>.Instance)
            .Handle(new EarnPointsCommand(_user, orderId ?? Guid.NewGuid(), amount), default);

    private Task<PointsRedemptionResult> Reserve(Guid orderId, decimal amount) =>
        new ReservePointsCommandHandler(_db, _db, _time).Handle(new ReservePointsCommand(orderId, _user, amount), default);

    private Task<LoyaltySummaryResult> Me() => new GetMyPointsQueryHandler(_db, _time).Handle(new GetMyPointsQuery(_user), default);

    [Fact]
    public async Task ElMismoEventoDosVeces_SumaUnaSolaVez()
    {
        var order = Guid.NewGuid();
        await Earn(120.75m, order);
        await Earn(120.75m, order);

        (await Me()).Balance.Should().Be(120);
    }

    [Fact]
    public async Task Reservar_ApartaLosPuntos_YReintentarLaMismaOrdenNoApartaDeNuevo()
    {
        await Earn(300m);
        var order = Guid.NewGuid();

        var first = await Reserve(order, 100m);
        var again = await Reserve(order, 100m);

        first.Should().Be(new PointsRedemptionResult(order, 300, 3m, "Reserved"));
        again.Should().Be(first);
        (await Me()).Balance.Should().Be(0);
        _db.Locks.Should().Be(2, "cada reserva bloquea la cuenta del cliente");
    }

    [Fact]
    public async Task SinPuntosSuficientes_ExplicaCuantosFaltan()
    {
        await Earn(80m);

        var act = () => Reserve(Guid.NewGuid(), 100m);

        (await act.Should().ThrowAsync<ConflictAppException>()).WithMessage("Necesitas al menos 100 puntos para usarlos (tienes 80).");
    }

    [Fact]
    public async Task LiberarDevuelveLosPuntos_YConfirmarLosDejaGastados()
    {
        await Earn(500m);
        var failed = Guid.NewGuid();
        var paid = Guid.NewGuid();

        await Reserve(failed, 100m);   // aparta los 500 (el tope de la mitad de $100 serían 5000)
        await new ReleasePointsCommandHandler(_db, _db, _time).Handle(new ReleasePointsCommand(failed, _user), default);
        (await Me()).Balance.Should().Be(500);

        await Reserve(paid, 400m);
        await new ConfirmPointsCommandHandler(_db, _db, _time).Handle(new ConfirmPointsCommand(paid, _user), default);
        var me = await Me();
        me.Balance.Should().Be(0);
        me.History.Should().HaveCount(2, "el canje deshecho no aparece en el historial");
        me.History.Select(h => h.Kind).Should().Contain(new[] { "Earned", "Redeemed" });
    }

    [Fact]
    public async Task OtroClienteNoPuedeConfirmarNiLiberarMiCanje()
    {
        await Earn(500m);
        var order = Guid.NewGuid();
        await Reserve(order, 300m);
        var intruder = Guid.NewGuid();

        var confirm = () => new ConfirmPointsCommandHandler(_db, _db, _time).Handle(new ConfirmPointsCommand(order, intruder), default);
        var release = () => new ReleasePointsCommandHandler(_db, _db, _time).Handle(new ReleasePointsCommand(order, intruder), default);

        await confirm.Should().ThrowAsync<ForbiddenAppException>();
        await release.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task Cotizar_ReflejaElTopeYElSaldo()
    {
        await Earn(5000m);

        var quote = await new QuoteRedemptionQueryHandler(_db, _time).Handle(new QuoteRedemptionQuery(_user, 30m), default);

        quote.Should().Be(new QuoteResult(5000, 1500, 15m));
    }

    [Fact]
    public async Task UnaReservaQueNuncaSePago_DevuelveLosPuntosALasDosHoras()
    {
        await Earn(500m);
        await Reserve(Guid.NewGuid(), 400m);
        (await Me()).Balance.Should().Be(0);

        _time.Now = _time.Now.AddHours(2).AddMinutes(1);

        (await Me()).Balance.Should().Be(500);
    }

    [Fact]
    public async Task NoSePuedenGastarDosVecesLosMismosPuntos_SiUnaReservaVencioYSeUsaronEnOtraCompra()
    {
        await Earn(1000m);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Reserve(a, 100m);                        // A aparta los 1000 (el tope de $100 serían 5000)…
        _time.Now = _time.Now.AddHours(2).AddMinutes(1); // …y no se paga a tiempo: vence.
        await Reserve(b, 2000m);                       // B usa los 1000 disponibles.

        // Antes de cobrar A, Órdenes renueva: ya no alcanza → no se cobra.
        var renewA = () => Reserve(a, 100m);

        (await renewA.Should().ThrowAsync<ConflictAppException>()).WithMessage("Tus puntos ya no alcanzan*");
    }

    [Fact]
    public async Task UnaReservaVencida_SeRenuevaSiTodaviaAlcanza()
    {
        await Earn(1000m);
        var a = Guid.NewGuid();
        await Reserve(a, 100m); // aparta los 1000
        _time.Now = _time.Now.AddHours(3);
        (await Me()).Balance.Should().Be(1000, "vencida, ya no descuenta");

        var renewed = await Reserve(a, 100m);

        renewed.Points.Should().Be(1000);
        (await Me()).Balance.Should().Be(0, "renovada, vuelve a descontar");
        (await Me()).History.Should().Contain(h => h.Kind == "Redeemed");
    }

    [Fact]
    public async Task AlRecibirOrderPaid_ElCanjeQuedaFirme_SinQueNadieLoPida()
    {
        await Earn(500m);
        var order = Guid.NewGuid();
        await Reserve(order, 400m);

        var result = await new ConfirmPointsCommandHandler(_db, _db, _time)
            .Handle(new ConfirmPointsCommand(order, RequesterId: null, OwnerId: _user), default);
        var none = await new ConfirmPointsCommandHandler(_db, _db, _time)
            .Handle(new ConfirmPointsCommand(Guid.NewGuid(), RequesterId: null, OwnerId: _user), default);

        result!.Status.Should().Be("Confirmed");
        none.Should().BeNull("una orden sin puntos no es un error en el evento");
        _time.Now = _time.Now.AddHours(5);
        (await Me()).Balance.Should().Be(0, "confirmado no vence");
    }

    [Fact]
    public async Task ElHistorialNoMuestraReservasVencidas()
    {
        await Earn(500m);
        await Reserve(Guid.NewGuid(), 400m);
        _time.Now = _time.Now.AddHours(3);

        (await Me()).History.Should().ContainSingle().Which.Kind.Should().Be("Earned");
    }
}

using FluentAssertions;
using Xunit;
using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;

namespace Ecommerce.Cart.UnitTests;

public class AbandonedCartRuleTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private static CartAggregate CartWithItem(string? email = "ana@ejemplo.com")
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Taza", "TZ-1", 8m, 1);
        cart.SetContact(email, "Ana");
        return cart;
    }

    [Fact]
    public void RecienModificado_NoEstaAbandonado()
    {
        var cart = CartWithItem();
        cart.NeedsAbandonedReminder(cart.UpdatedAtUtc.AddMinutes(59), Hour, Day).Should().BeFalse();
        cart.NeedsAbandonedReminder(cart.UpdatedAtUtc.AddMinutes(60), Hour, Day).Should().BeTrue();
    }

    [Fact]
    public void YaRecordado_NoSeRepiteHastaQueVuelvaACambiar()
    {
        var cart = CartWithItem();
        var later = cart.UpdatedAtUtc.AddHours(2);
        cart.MarkAbandonedReminderSent();

        cart.NeedsAbandonedReminder(later.AddHours(1), Hour, Day).Should().BeFalse();

        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Plato", "PL-1", 5m, 1); // vuelve y lo cambia
        cart.NeedsAbandonedReminder(cart.UpdatedAtUtc.AddHours(1), Hour, Day).Should().BeTrue();
    }

    [Fact]
    public void SinEmailOSinProductos_OMuyViejo_NoSeRecuerda()
    {
        var sinEmail = CartWithItem(email: null);
        sinEmail.NeedsAbandonedReminder(sinEmail.UpdatedAtUtc.AddHours(2), Hour, Day).Should().BeFalse();

        var vacio = CartWithItem();
        vacio.Clear();
        vacio.NeedsAbandonedReminder(vacio.UpdatedAtUtc.AddHours(2), Hour, Day).Should().BeFalse();

        var viejo = CartWithItem();
        viejo.NeedsAbandonedReminder(viejo.UpdatedAtUtc.AddHours(25), Hour, Day).Should().BeFalse();
    }

    [Fact]
    public void SetContact_NoBorraLoQueYaHabiaConValoresVacios()
    {
        var cart = CartWithItem();
        cart.SetContact(null, "  ");

        cart.ContactEmail.Should().Be("ana@ejemplo.com");
        cart.ContactName.Should().Be("Ana");
    }
}

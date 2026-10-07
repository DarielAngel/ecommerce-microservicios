using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Features;
using Ecommerce.Contracts.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Cart.IntegrationTests;

/// <summary>
/// Recordatorio de carrito abandonado contra la base real. En vez de esperar una hora, se le pasa al
/// comando un "ahora" en el futuro.
/// </summary>
[Collection(CartApiCollection.Name)]
public class AbandonedCartTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);
    private readonly CartApiFactory _factory;

    public AbandonedCartTests(CartApiFactory factory) => _factory = factory;

    private async Task<(Guid UserId, string Email)> CustomerWithCartAsync(int quantity = 2)
    {
        var userId = Guid.NewGuid();
        var email = $"carrito-{userId:N}@ejemplo.com";
        var variantId = Guid.NewGuid();
        _factory.FakeCatalog.Variants[variantId] = new VariantInfo(variantId, Guid.NewGuid(), "Taza de cerámica", "TZ-1", 8.5m, true);
        _factory.FakeInventory.AvailableQuantities[variantId] = 10;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.CreateToken(userId, email: email, fullName: "Ana Martínez"));
        (await client.PostAsJsonAsync("/api/cart/items", new { VariantId = variantId, Quantity = quantity })).EnsureSuccessStatusCode();
        return (userId, email);
    }

    private async Task<int> DetectAsync(DateTime now, int batchSize = 100)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DetectAbandonedCartsCommand(now, Hour, Day, batchSize));
    }

    private CartAbandonedEvent[] EventsFor(Guid userId) =>
        _factory.Published.Events.OfType<CartAbandonedEvent>().Where(e => e.UserId == userId).ToArray();

    [Fact]
    public async Task UnCarritoQuieto_MasDeUnaHora_RecibeUnSoloRecordatorioConSusProductos()
    {
        var (userId, email) = await CustomerWithCartAsync();

        await DetectAsync(DateTime.UtcNow.AddMinutes(30));      // todavía no: lleva media hora
        EventsFor(userId).Should().BeEmpty();

        await DetectAsync(DateTime.UtcNow.AddMinutes(61));
        await DetectAsync(DateTime.UtcNow.AddMinutes(90));       // no se repite

        var reminder = EventsFor(userId).Should().ContainSingle().Subject;
        reminder.Email.Should().Be(email);
        reminder.FullName.Should().Be("Ana Martínez");
        reminder.Items.Should().ContainSingle().Which.Should().Match<AbandonedCartItem>(i =>
            i.ProductName == "Taza de cerámica" && i.Quantity == 2 && i.UnitPrice == 8.5m);
        reminder.Subtotal.Should().Be(17m);
    }

    [Fact]
    public async Task SiVuelveYLoCambia_SePuedeRecordarDeNuevoMasAdelante()
    {
        var (userId, _) = await CustomerWithCartAsync();
        await DetectAsync(DateTime.UtcNow.AddMinutes(61));

        // Vuelve y agrega otra unidad: el carrito "despierta".
        await CustomerAddsAgainAsync(userId);
        await DetectAsync(DateTime.UtcNow.AddMinutes(30));
        EventsFor(userId).Should().HaveCount(1);

        await DetectAsync(DateTime.UtcNow.AddMinutes(61));
        EventsFor(userId).Should().HaveCount(2);
        EventsFor(userId).Select(e => e.ReminderId).Distinct().Should().HaveCount(2, "cada recordatorio tiene su id");
    }

    [Fact]
    public async Task UnCarritoDeHaceMasDeUnDia_NoSeRecuerda()
    {
        var (userId, _) = await CustomerWithCartAsync();

        await DetectAsync(DateTime.UtcNow.AddHours(25));

        EventsFor(userId).Should().BeEmpty();
    }

    [Fact]
    public async Task UnCarritoVacio_NoSeRecuerda()
    {
        var (userId, _) = await CustomerWithCartAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(userId));
        (await client.DeleteAsync("/api/cart")).EnsureSuccessStatusCode();

        await DetectAsync(DateTime.UtcNow.AddMinutes(61));

        EventsFor(userId).Should().BeEmpty();
    }

    [Fact]
    public async Task LosYaAvisados_NoOcupanElLote_YLosNuevosIgualRecibenSuCorreo()
    {
        // El primero ya recibió su recordatorio y es el más viejo: si siguiera saliendo en la consulta,
        // con lotes de 1 taparía para siempre al segundo.
        var (first, _) = await CustomerWithCartAsync();
        await DetectAsync(DateTime.UtcNow.AddMinutes(61));
        EventsFor(first).Should().ContainSingle();
        var (second, _) = await CustomerWithCartAsync();

        var now = DateTime.UtcNow.AddMinutes(61);
        for (var i = 0; i < 500 && await DetectAsync(now, batchSize: 1) > 0; i++) { }

        EventsFor(second).Should().ContainSingle();
        EventsFor(first).Should().ContainSingle();
    }

    private async Task CustomerAddsAgainAsync(Guid userId)
    {
        var variantId = Guid.NewGuid();
        _factory.FakeCatalog.Variants[variantId] = new VariantInfo(variantId, Guid.NewGuid(), "Plato", "PL-1", 5m, true);
        _factory.FakeInventory.AvailableQuantities[variantId] = 10;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(userId));
        (await client.PostAsJsonAsync("/api/cart/items", new { VariantId = variantId, Quantity = 1 })).EnsureSuccessStatusCode();
    }
}

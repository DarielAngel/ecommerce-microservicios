using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Contracts.Events;
using Ecommerce.Loyalty.Application.Features;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Loyalty.IntegrationTests;

public class LoyaltyApiTests : IClassFixture<LoyaltyApiFactory>
{
    private readonly LoyaltyApiFactory _factory;

    public LoyaltyApiTests(LoyaltyApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LoyaltyApiFactory.CreateToken(userId));
        return client;
    }

    private async Task PublishOrderPaidAsync(Guid userId, Guid orderId, decimal total)
    {
        // Si se publica antes de que el consumidor haya creado y enlazado su cola, RabbitMQ descarta el
        // mensaje (nadie lo escucha todavía): esperamos a que el bus esté listo.
        await _factory.Services.GetRequiredService<IBusControl>()
            .WaitForHealthStatus(BusHealthStatus.Healthy, TimeSpan.FromSeconds(30));

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new OrderPaidEvent(
            orderId, userId, "cliente@ejemplo.com", "Cliente", total, "USD", DateTime.UtcNow));
    }

    private static async Task<LoyaltySummaryResult> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<LoyaltySummaryResult>("/api/loyalty/me"))!;

    private static async Task<int> WaitForBalanceAsync(HttpClient client, int expected, int timeoutMs = 20000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int balance;
        do
        {
            balance = (await MeAsync(client)).Balance;
            if (balance == expected) return balance;
            await Task.Delay(250);
        } while (DateTime.UtcNow < until);
        return balance;
    }

    [Fact]
    public async Task SinSesion_Devuelve401()
    {
        (await _factory.CreateClient().GetAsync("/api/loyalty/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClienteNuevo_SaldoCero_YLasReglasDelPrograma()
    {
        var me = await MeAsync(ClientFor(Guid.NewGuid()));

        me.Balance.Should().Be(0);
        me.History.Should().BeEmpty();
        me.Rules.Should().Be(new LoyaltyRulesResult(1, 0.01m, 100, 0.5m));
    }

    [Fact]
    public async Task UnaCompraPagada_SumaPuntos_UnaSolaVezAunqueElEventoLlegueDuplicado()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        var order = Guid.NewGuid();

        await PublishOrderPaidAsync(user, order, 249.99m);
        await PublishOrderPaidAsync(user, order, 249.99m);

        (await WaitForBalanceAsync(client, 249)).Should().Be(249);
        await Task.Delay(1500); // margen para que el duplicado se procese
        var me = await MeAsync(client);
        me.Balance.Should().Be(249);
        me.BalanceValue.Should().Be(2.49m);
        me.History.Should().ContainSingle().Which.Kind.Should().Be("Earned");
    }

    [Fact]
    public async Task Canje_ReservarConfirmar_YLiberarDevuelve()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await PublishOrderPaidAsync(user, Guid.NewGuid(), 800m);
        await WaitForBalanceAsync(client, 800);

        // Cotización: compra de $10 → como mucho $5 (500 puntos).
        var quote = await client.GetFromJsonAsync<QuoteResult>("/api/loyalty/me/quote?amount=10");
        quote.Should().Be(new QuoteResult(800, 500, 5m));

        var failed = Guid.NewGuid();
        var reserve = await client.PostAsJsonAsync("/internal/loyalty/redemptions", new { OrderId = failed, Amount = 10m });
        reserve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reserve.Content.ReadFromJsonAsync<PointsRedemptionResult>())!.Points.Should().Be(500);
        (await MeAsync(client)).Balance.Should().Be(300);

        (await client.PostAsync($"/internal/loyalty/redemptions/{failed}/release", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MeAsync(client)).Balance.Should().Be(800);

        var paid = Guid.NewGuid();
        await client.PostAsJsonAsync("/internal/loyalty/redemptions", new { OrderId = paid, Amount = 100m });
        var confirm = await client.PostAsync($"/internal/loyalty/redemptions/{paid}/confirm", null);
        (await confirm.Content.ReadFromJsonAsync<PointsRedemptionResult>())!.Status.Should().Be("Confirmed");
        (await MeAsync(client)).Balance.Should().Be(0);
    }

    [Fact]
    public async Task SinPuntosSuficientes_Devuelve409ConElMotivo()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/internal/loyalty/redemptions", new { OrderId = Guid.NewGuid(), Amount = 50m });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("message").GetString()
            .Should().Be("Necesitas al menos 100 puntos para usarlos (tienes 0).");
    }

    [Fact]
    public async Task DosCheckoutsALaVez_NoGastanLosMismosPuntosDosVeces()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await PublishOrderPaidAsync(user, Guid.NewGuid(), 300m);
        await WaitForBalanceAsync(client, 300);

        // Cada checkout querría usar los 300 puntos (compras de $100): solo uno puede.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            ClientFor(user).PostAsJsonAsync("/internal/loyalty/redemptions", new { OrderId = Guid.NewGuid(), Amount = 100m })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
        (await MeAsync(client)).Balance.Should().Be(0);
    }

    [Fact]
    public async Task ElCanjeDeOtroCliente_NoSePuedeTocar()
    {
        var owner = Guid.NewGuid();
        var ownerClient = ClientFor(owner);
        await PublishOrderPaidAsync(owner, Guid.NewGuid(), 500m);
        await WaitForBalanceAsync(ownerClient, 500);
        var order = Guid.NewGuid();
        await ownerClient.PostAsJsonAsync("/internal/loyalty/redemptions", new { OrderId = order, Amount = 100m });

        var intruder = ClientFor(Guid.NewGuid());
        (await intruder.PostAsync($"/internal/loyalty/redemptions/{order}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await intruder.PostAsync($"/internal/loyalty/redemptions/{order}/release", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

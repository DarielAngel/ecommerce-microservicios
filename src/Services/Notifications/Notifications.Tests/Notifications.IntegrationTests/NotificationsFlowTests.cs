using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Contracts.Events;
using Ecommerce.Notifications.Application.Features;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Notifications.IntegrationTests;

public class NotificationsFlowTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationsFlowTests(NotificationsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task PublishAsync<T>(T message) where T : class
    {
        // Si se publica antes de que el consumidor haya creado y enlazado su cola, RabbitMQ descarta el
        // mensaje (nadie lo escucha todavía) y la prueba falla al azar: esperamos a que el bus esté listo.
        await _factory.Services.GetRequiredService<IBusControl>()
            .WaitForHealthStatus(BusHealthStatus.Healthy, TimeSpan.FromSeconds(30));
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(message);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 20000)
    {
        var waited = 0;
        while (waited < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(200);
            waited += 200;
        }
        return condition();
    }

    private int SentTo(string email) => _factory.FakeEmail.Sent.Count(e => e.To == email);

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventoUserRegistered_DeberiaEnviarElEmailDeBienvenida()
    {
        var email = $"{Guid.NewGuid():N}@test.com";

        await PublishAsync(new UserRegisteredEvent(Guid.NewGuid(), email, "Ana Gómez", DateTime.UtcNow));

        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue("el consumidor debería haber enviado el email");
        _factory.FakeEmail.Sent.First(e => e.To == email).Subject.Should().Contain("Bienvenido");
    }

    [Fact]
    public async Task EventoOrderPaid_DeberiaEnviarLaConfirmacionConElTotal()
    {
        var email = $"{Guid.NewGuid():N}@test.com";

        await PublishAsync(new OrderPaidEvent(Guid.NewGuid(), Guid.NewGuid(), email, "Ana", 75.5m, "USD", DateTime.UtcNow));

        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue();
        _factory.FakeEmail.Sent.First(e => e.To == email).Html.Should().Contain("75.50 USD");
    }

    [Fact]
    public async Task EventoOrderShipped_DeberiaEnviarElAvisoDeEnvio()
    {
        var email = $"{Guid.NewGuid():N}@test.com";

        await PublishAsync(new OrderShippedEvent(Guid.NewGuid(), Guid.NewGuid(), email, "Ana", DateTime.UtcNow));

        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue();
        _factory.FakeEmail.Sent.First(e => e.To == email).Subject.Should().Contain("va en camino");
    }

    [Fact]
    public async Task EventoDuplicado_DeberiaEnviarUnSoloEmail()
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        var orderId = Guid.NewGuid();
        var paidEvent = new OrderPaidEvent(orderId, Guid.NewGuid(), email, "Ana", 10m, "USD", DateTime.UtcNow);

        // Mismo evento publicado 3 veces (simula redelivery de RabbitMQ / doble publicación).
        await PublishAsync(paidEvent);
        await PublishAsync(paidEvent);
        await PublishAsync(paidEvent);

        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue();
        await Task.Delay(3000); // margen para que los duplicados se procesen (y sean ignorados)

        SentTo(email).Should().Be(1, "la idempotencia debe evitar emails duplicados");
    }

    [Fact]
    public async Task ListarNotificaciones_SinToken_DeberiaDevolver401()
    {
        var response = await _client.GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListarNotificaciones_ConRolCliente_DeberiaDevolver403()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NotificationsApiFactory.CreateToken("Cliente"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListarNotificaciones_ConRolAdmin_DeberiaIncluirLasEnviadas()
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        await PublishAsync(new UserRegisteredEvent(Guid.NewGuid(), email, "Ana", DateTime.UtcNow));
        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications?count=200");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NotificationsApiFactory.CreateToken("Admin"));

        var response = await _client.SendAsync(request);
        var list = await response.Content.ReadFromJsonAsync<List<SentNotificationResult>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Should().Contain(n => n.RecipientEmail == email && n.Type == "UserRegistered");
    }

    [Fact]
    public async Task EventoCartAbandoned_EnviaUnSoloRecordatorioAunqueLleguenDuplicados()
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        var reminder = new CartAbandonedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), email, "Ana",
            new List<AbandonedCartItem> { new(Guid.NewGuid(), "Taza de cerámica", 2, 8.5m) },
            17m, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow);

        await PublishAsync(reminder);
        await PublishAsync(reminder);

        (await WaitUntilAsync(() => SentTo(email) >= 1)).Should().BeTrue();
        await Task.Delay(1500);
        SentTo(email).Should().Be(1);
        var sent = _factory.FakeEmail.Sent.First(e => e.To == email);
        sent.Subject.Should().Be("Dejaste productos en tu carrito");
        // (HtmlEncode escribe la "á" como entidad; el correo se ve igual.)
        sent.Html.Should().Contain("2× Taza de cer").And.Contain("/cart");
    }
}

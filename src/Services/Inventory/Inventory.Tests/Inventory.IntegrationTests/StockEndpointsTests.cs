using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Contracts.Events;
using Ecommerce.Inventory.Application.Features;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Inventory.IntegrationTests;

public class StockEndpointsTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public StockEndpointsTests(InventoryApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> SeedStockItemAsync(int quantityOnHand = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());
        if (quantityOnHand > 0)
        {
            stockItem.SetQuantityOnHand(quantityOnHand);
        }

        dbContext.StockItems.Add(stockItem);
        await dbContext.SaveChangesAsync();

        return stockItem.VariantId;
    }

    private HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStock_ConVarianteExistente_DeberiaDevolverElStock()
    {
        var variantId = await SeedStockItemAsync(quantityOnHand: 40);
        var token = TestJwtFactory.CreateToken("Cliente");

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/stock/{variantId}", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stock = await response.Content.ReadFromJsonAsync<StockResult>();
        stock!.QuantityOnHand.Should().Be(40);
    }

    [Fact]
    public async Task GetStock_ConVarianteInexistente_DeberiaDevolver404()
    {
        var token = TestJwtFactory.CreateToken("Cliente");

        var response = await _client.SendAsync(
            WithAuth(HttpMethod.Get, $"/api/stock/{Guid.NewGuid()}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStock_SinToken_DeberiaDevolver401()
    {
        var response = await _client.GetAsync($"/api/stock/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdjustStock_ConRolAdmin_DeberiaActualizarElStock()
    {
        var variantId = await SeedStockItemAsync();
        var token = TestJwtFactory.CreateToken("Admin");

        var request = WithAuth(HttpMethod.Post, $"/api/stock/{variantId}/adjust", token);
        request.Content = JsonContent.Create(new { NewQuantityOnHand = 75 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stock = await response.Content.ReadFromJsonAsync<StockResult>();
        stock!.QuantityOnHand.Should().Be(75);
    }

    [Fact]
    public async Task AdjustStock_ConRolCliente_DeberiaDevolver403()
    {
        var variantId = await SeedStockItemAsync();
        var token = TestJwtFactory.CreateToken("Cliente");

        var request = WithAuth(HttpMethod.Post, $"/api/stock/{variantId}/adjust", token);
        request.Content = JsonContent.Create(new { NewQuantityOnHand = 75 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FlujoCompletoDeReserva_ReservarConfirmar_DeberiaDescontarStockDefinitivamente()
    {
        var variantId = await SeedStockItemAsync(quantityOnHand: 10);
        var orderId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken("Admin");

        var reserveRequest = WithAuth(HttpMethod.Post, "/api/stock/reservations", token);
        reserveRequest.Content = JsonContent.Create(new
        {
            OrderId = orderId,
            Items = new[] { new { VariantId = variantId, Quantity = 3 } }
        });
        var reserveResponse = await _client.SendAsync(reserveRequest);
        reserveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterReserve = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/stock/{variantId}", token));
        var stockAfterReserve = await afterReserve.Content.ReadFromJsonAsync<StockResult>();
        stockAfterReserve!.QuantityAvailable.Should().Be(7); // 10 - 3 reservadas
        stockAfterReserve.QuantityOnHand.Should().Be(10); // todavía no se confirmó

        var confirmResponse = await _client.SendAsync(
            WithAuth(HttpMethod.Post, $"/api/stock/reservations/{orderId}/confirm", token));
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterConfirm = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/stock/{variantId}", token));
        var stockAfterConfirm = await afterConfirm.Content.ReadFromJsonAsync<StockResult>();
        stockAfterConfirm!.QuantityOnHand.Should().Be(7); // se descontó definitivamente
        stockAfterConfirm.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task FlujoCompletoDeReserva_ReservarYLiberar_DeberiaDevolverElStockDisponible()
    {
        var variantId = await SeedStockItemAsync(quantityOnHand: 10);
        var orderId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken("Admin");

        var reserveRequest = WithAuth(HttpMethod.Post, "/api/stock/reservations", token);
        reserveRequest.Content = JsonContent.Create(new
        {
            OrderId = orderId,
            Items = new[] { new { VariantId = variantId, Quantity = 4 } }
        });
        await _client.SendAsync(reserveRequest);

        var releaseResponse = await _client.SendAsync(
            WithAuth(HttpMethod.Post, $"/api/stock/reservations/{orderId}/release", token));
        releaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRelease = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/stock/{variantId}", token));
        var stock = await afterRelease.Content.ReadFromJsonAsync<StockResult>();
        stock!.QuantityOnHand.Should().Be(10); // el físico no cambia
        stock.QuantityAvailable.Should().Be(10); // vuelve a estar disponible
    }

    [Fact]
    public async Task Reserve_SinStockSuficiente_DeberiaDevolver409()
    {
        var variantId = await SeedStockItemAsync(quantityOnHand: 2);
        var token = TestJwtFactory.CreateToken("Admin");

        var request = WithAuth(HttpMethod.Post, "/api/stock/reservations", token);
        request.Content = JsonContent.Create(new
        {
            OrderId = Guid.NewGuid(),
            Items = new[] { new { VariantId = variantId, Quantity = 999 } }
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EventoVariantCreated_DeberiaCrearAutomaticamenteElRegistroDeStockEnCero()
    {
        // Publica el evento directamente al bus (simulando lo que hace Catálogo al crear una
        // variante), y espera a que el consumidor de Inventario lo procese de forma asíncrona.
        var variantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new VariantCreatedEvent(
                variantId, Guid.NewGuid(), "SKU-TEST-EVENTO", DateTime.UtcNow));
        }

        var token = TestJwtFactory.CreateToken("Cliente");

        // Espera activa con timeout: el consumo es asíncrono, no instantáneo.
        StockResult? stock = null;
        for (var i = 0; i < 20 && stock is null; i++)
        {
            await Task.Delay(500);
            var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/stock/{variantId}", token));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                stock = await response.Content.ReadFromJsonAsync<StockResult>();
            }
        }

        stock.Should().NotBeNull("el consumidor de RabbitMQ debería haber creado el registro de stock");
        stock!.QuantityOnHand.Should().Be(0);
    }
}

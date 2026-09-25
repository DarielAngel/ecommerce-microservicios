using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Cart.IntegrationTests;

public class CartEndpointsTests : IClassFixture<CartApiFactory>
{
    private readonly CartApiFactory _factory;
    private readonly HttpClient _client;

    public CartEndpointsTests(CartApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private VariantInfo SeedVariant(int availableQuantity, decimal price = 20m)
    {
        var variantId = Guid.NewGuid();
        var variant = new VariantInfo(variantId, Guid.NewGuid(), "Producto de Prueba", $"SKU-{variantId:N}", price, IsActive: true);

        _factory.FakeCatalog.Variants[variantId] = variant;
        _factory.FakeInventory.AvailableQuantities[variantId] = availableQuantity;

        return variant;
    }

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCart_SinToken_DeberiaDevolver401()
    {
        var response = await _client.GetAsync("/api/cart");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCart_UsuarioSinCarritoTodavia_DeberiaDevolverCarritoVacio()
    {
        var token = TestJwtFactory.CreateToken(Guid.NewGuid());

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/cart", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().BeEmpty();
        cart.Subtotal.Should().Be(0m);
    }

    [Fact]
    public async Task AddItem_ConStockSuficiente_DeberiaAgregarloYCongelarElPrecio()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 10, price: 25m);

        var request = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        request.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 3 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().ContainSingle();
        cart.Items[0].UnitPrice.Should().Be(25m);
        cart.Items[0].Quantity.Should().Be(3);
        cart.Subtotal.Should().Be(75m);
    }

    [Fact]
    public async Task AddItem_DosVecesLaMismaVariante_DeberiaAcumularEnUnaSolaLinea()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 10, price: 10m);

        var firstRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        firstRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 2 });
        await _client.SendAsync(firstRequest);

        var secondRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        secondRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 3 });
        var response = await _client.SendAsync(secondRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().ContainSingle();
        cart.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task AddItem_ConStockInsuficiente_DeberiaDevolver409()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 2);

        var request = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        request.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 5 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddItem_ConVarianteQueNoExisteEnCatalogo_DeberiaDevolver404()
    {
        var token = TestJwtFactory.CreateToken(Guid.NewGuid());

        var request = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        request.Content = JsonContent.Create(new { VariantId = Guid.NewGuid(), Quantity = 1 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateQuantity_ConValorValido_DeberiaActualizarLaLinea()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 10);

        var addRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        addRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 2 });
        await _client.SendAsync(addRequest);

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/cart/items/{variant.VariantId}", token);
        updateRequest.Content = JsonContent.Create(new { Quantity = 7 });
        var response = await _client.SendAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items[0].Quantity.Should().Be(7);
    }

    [Fact]
    public async Task UpdateQuantity_ConCero_DeberiaQuitarLaLinea()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 10);

        var addRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        addRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 2 });
        await _client.SendAsync(addRequest);

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/cart/items/{variant.VariantId}", token);
        updateRequest.Content = JsonContent.Create(new { Quantity = 0 });
        var response = await _client.SendAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveItem_DeberiaQuitarLaLinea()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variant = SeedVariant(availableQuantity: 10);

        var addRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
        addRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 2 });
        await _client.SendAsync(addRequest);

        var response = await _client.SendAsync(
            WithAuth(HttpMethod.Delete, $"/api/cart/items/{variant.VariantId}", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearCart_DeberiaVaciarTodasLasLineas()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtFactory.CreateToken(userId);
        var variantA = SeedVariant(availableQuantity: 10);
        var variantB = SeedVariant(availableQuantity: 10);

        foreach (var variant in new[] { variantA, variantB })
        {
            var addRequest = WithAuth(HttpMethod.Post, "/api/cart/items", token);
            addRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 1 });
            await _client.SendAsync(addRequest);
        }

        var response = await _client.SendAsync(WithAuth(HttpMethod.Delete, "/api/cart", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResult>();
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DosUsuariosDistintos_DeberianTenerCarritosIndependientes()
    {
        var user1Token = TestJwtFactory.CreateToken(Guid.NewGuid());
        var user2Token = TestJwtFactory.CreateToken(Guid.NewGuid());
        var variant = SeedVariant(availableQuantity: 10);

        var addRequest = WithAuth(HttpMethod.Post, "/api/cart/items", user1Token);
        addRequest.Content = JsonContent.Create(new { VariantId = variant.VariantId, Quantity = 4 });
        await _client.SendAsync(addRequest);

        var user2CartResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/cart", user2Token));
        var user2Cart = await user2CartResponse.Content.ReadFromJsonAsync<CartResult>();

        user2Cart!.Items.Should().BeEmpty("el carrito del usuario 2 no debería verse afectado por lo que hizo el usuario 1");
    }
}

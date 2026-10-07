using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Wishlist.Domain.Entities;
using Ecommerce.Wishlist.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Wishlist.IntegrationTests;

public class WishlistApiTests : IClassFixture<WishlistApiFactory>
{
    private readonly WishlistApiFactory _factory;
    private readonly HttpClient _client;

    public WishlistApiTests(WishlistApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record ItemDto(Guid ProductId, DateTime AddedAtUtc);

    // Cada test usa su propio usuario: comparten base de datos pero nunca se pisan.
    private static string TokenFor(Guid userId) => WishlistApiFactory.CreateToken(userId);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> AddAsync(Guid productId, string? token) =>
        SendAsync(HttpMethod.Put, $"/api/wishlist/{productId}", token);

    private Task<HttpResponseMessage> RemoveAsync(Guid productId, string? token) =>
        SendAsync(HttpMethod.Delete, $"/api/wishlist/{productId}", token);

    private async Task<List<ItemDto>> ListAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/wishlist", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<ItemDto>>())!;
    }

    // ---- Salud y autenticación ----

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET", "/api/wishlist")]
    [InlineData("PUT", "/api/wishlist/3f2b8c1e-0000-4000-8000-000000000001")]
    [InlineData("DELETE", "/api/wishlist/3f2b8c1e-0000-4000-8000-000000000001")]
    public async Task SinToken_DeberiaResponder401(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, token: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- Agregar / listar ----

    [Fact]
    public async Task Agregar_DeberiaAparecerEnMisFavoritos()
    {
        var token = TokenFor(Guid.NewGuid());
        var productId = Guid.NewGuid();

        (await AddAsync(productId, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var items = await ListAsync(token);
        items.Should().ContainSingle().Which.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task AgregarDosVeces_NoDeberiaDuplicar()
    {
        var token = TokenFor(Guid.NewGuid());
        var productId = Guid.NewGuid();

        (await AddAsync(productId, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AddAsync(productId, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListAsync(token)).Should().ContainSingle();
    }

    [Fact]
    public async Task AgregarElMismoProductoEnParalelo_NoDeberiaFallarNiDuplicar()
    {
        // Doble clic rápido en el corazón: varias requests llegan a la vez y deben resolverse en la base.
        var token = TokenFor(Guid.NewGuid());
        var productId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AddAsync(productId, token)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().ContainSingle();
    }

    [Fact]
    public async Task Listar_DeberiaOrdenarDelMasRecienteAlMasAntiguo()
    {
        var token = TokenFor(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        foreach (var id in new[] { first, second, third })
        {
            (await AddAsync(id, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            await Task.Delay(20); // fechas distintas aunque el reloj tenga poca resolución
        }

        (await ListAsync(token)).Select(i => i.ProductId).Should().Equal(third, second, first);
    }

    [Fact]
    public async Task UsuarioSinFavoritos_DeberiaRecibirListaVacia()
    {
        (await ListAsync(TokenFor(Guid.NewGuid()))).Should().BeEmpty();
    }

    // ---- Quitar ----

    [Fact]
    public async Task Quitar_DeberiaDesaparecerDeMisFavoritos()
    {
        var token = TokenFor(Guid.NewGuid());
        var keep = Guid.NewGuid();
        var remove = Guid.NewGuid();
        await AddAsync(keep, token);
        await AddAsync(remove, token);

        (await RemoveAsync(remove, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListAsync(token)).Should().ContainSingle().Which.ProductId.Should().Be(keep);
    }

    [Fact]
    public async Task QuitarAlgoQueNoEsta_DeberiaResponder204()
    {
        var response = await RemoveAsync(Guid.NewGuid(), TokenFor(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- Privacidad ----

    [Fact]
    public async Task CadaUsuario_SoloVeYTocaSusPropiosFavoritos()
    {
        var ana = TokenFor(Guid.NewGuid());
        var beto = TokenFor(Guid.NewGuid());
        var productId = Guid.NewGuid();
        await AddAsync(productId, ana);

        (await ListAsync(beto)).Should().BeEmpty("Beto no debe ver los favoritos de Ana");

        // Beto "quita" el mismo producto: solo afecta a SU lista (vacía), nunca a la de Ana.
        await RemoveAsync(productId, beto);
        (await ListAsync(ana)).Should().ContainSingle().Which.ProductId.Should().Be(productId);
    }

    // ---- Tope ----

    [Fact]
    public async Task ConLaListaLlena_AgregarUnoNuevoDeberiaResponder409_PeroRepetirUnoExistenteNo()
    {
        var userId = Guid.NewGuid();
        var token = TokenFor(userId);
        var existing = Guid.NewGuid();

        // Llenamos la lista directo en la base: 100 requests HTTP harían el test lento sin probar nada extra.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WishlistDbContext>();
            db.Items.Add(WishlistItem.Create(userId, existing));
            for (var i = 1; i < WishlistItem.MaxItemsPerUser; i++)
                db.Items.Add(WishlistItem.Create(userId, Guid.NewGuid()));
            await db.SaveChangesAsync();
            (await db.Items.CountAsync(x => x.UserId == userId)).Should().Be(WishlistItem.MaxItemsPerUser);
        }

        var full = await AddAsync(Guid.NewGuid(), token);
        full.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await full.Content.ReadAsStringAsync()).Should().Contain("llena");

        (await AddAsync(existing, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ProductIdInvalido_DeberiaResponder404PorLaRestriccionDeRuta()
    {
        var response = await AddAsync_Raw("no-es-un-guid", TokenFor(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProductIdVacio_DeberiaResponder400()
    {
        var response = await AddAsync(Guid.Empty, TokenFor(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Task<HttpResponseMessage> AddAsync_Raw(string productId, string token) =>
        SendAsync(HttpMethod.Put, $"/api/wishlist/{productId}", token);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Contracts.Events;
using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Reviews.IntegrationTests;

public class ReviewsApiTests : IClassFixture<ReviewsApiFactory>
{
    private readonly ReviewsApiFactory _factory;
    private readonly HttpClient _client;

    public ReviewsApiTests(ReviewsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- DTOs de lectura (reflejan el JSON público de la API) ----
    private record ReviewDto(Guid Id, Guid ProductId, string AuthorName, int Rating, string Title, string Comment,
        bool IsVerifiedPurchase, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

    private record PageDto(List<ReviewDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    private record SummaryDto(Guid ProductId, double Average, int Count, Dictionary<int, int> Distribution);

    // ---- Helpers ----
    private static string TokenFor(Guid userId, string role = "Cliente", string name = "Ana Pérez Gómez") =>
        ReviewsApiFactory.CreateToken(userId, role, name);

    private HttpRequestMessage Request(HttpMethod method, string url, string? token = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private Task<HttpResponseMessage> PostReviewAsync(Guid productId, string? token, int rating = 5, string title = "Excelente", string? comment = "Muy bueno") =>
        _client.SendAsync(Request(HttpMethod.Post, $"/api/reviews/products/{productId}", token, new { rating, title, comment }));

    private async Task<ReviewDto> CreateAsync(Guid productId, Guid userId, int rating = 5, string title = "Excelente")
    {
        var response = await PostReviewAsync(productId, TokenFor(userId), rating, title);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private async Task PublishOrderPaidAsync(Guid userId, params Guid[]? productIds)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new OrderPaidEvent(
            Guid.NewGuid(), userId, "x@test.com", "Ana", 10m, "USD", DateTime.UtcNow, productIds));
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 20000)
    {
        for (var waited = 0; waited < timeoutMs; waited += 250)
        {
            if (await condition()) return true;
            await Task.Delay(250);
        }
        return await condition();
    }

    private async Task<bool> HasPurchasedAsync(Guid userId, Guid productId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IVerifiedPurchaseRepository>()
            .HasPurchasedAsync(userId, productId, CancellationToken.None);
    }

    // ---- Salud ----
    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        (await _client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Crear ----
    [Fact]
    public async Task Crear_ConToken_DeberiaDevolver201ConNombreProtegido()
    {
        var productId = Guid.NewGuid();

        var response = await PostReviewAsync(productId, TokenFor(Guid.NewGuid(), name: "Ana Pérez Gómez"), 4, "Muy bueno", "Cumple");
        var review = await response.Content.ReadFromJsonAsync<ReviewDto>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        review!.ProductId.Should().Be(productId);
        review.Rating.Should().Be(4);
        review.AuthorName.Should().Be("Ana P.");
        review.IsVerifiedPurchase.Should().BeFalse();
    }

    [Fact]
    public async Task Crear_SinToken_DeberiaDevolver401()
    {
        (await PostReviewAsync(Guid.NewGuid(), token: null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Crear_ConCalificacionInvalida_DeberiaDevolver400()
    {
        (await PostReviewAsync(Guid.NewGuid(), TokenFor(Guid.NewGuid()), rating: 6)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Crear_SinTitulo_DeberiaDevolver400()
    {
        (await PostReviewAsync(Guid.NewGuid(), TokenFor(Guid.NewGuid()), title: "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Crear_DosVecesElMismoProducto_DeberiaDevolver409()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await CreateAsync(productId, userId);

        var second = await PostReviewAsync(productId, TokenFor(userId), 1, "Otra vez");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Crear_DosRequestsSimultaneos_SoloUnoDeberiaGanar()
    {
        // La restricción UNIQUE (user_id, product_id) es la garantía real contra la carrera.
        var productId = Guid.NewGuid();
        var token = TokenFor(Guid.NewGuid());

        var responses = await Task.WhenAll(
            PostReviewAsync(productId, token, 5, "A"),
            PostReviewAsync(productId, token, 4, "B"));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
    }

    // ---- Listar (público) ----
    [Fact]
    public async Task Listar_SinToken_DeberiaDevolverLasReseñasSinExponerElIdDelUsuario()
    {
        var productId = Guid.NewGuid();
        await CreateAsync(productId, Guid.NewGuid(), 5, "Público");

        var response = await _client.GetAsync($"/api/reviews/products/{productId}");
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().Contain("Público");
        raw.ToLowerInvariant().Should().NotContain("userid", "la API pública no debe exponer identificadores de usuarios");
    }

    [Fact]
    public async Task Listar_DeberiaOrdenarPorMejorYPeorCalificadas()
    {
        var productId = Guid.NewGuid();
        await CreateAsync(productId, Guid.NewGuid(), 3, "Tres");
        await CreateAsync(productId, Guid.NewGuid(), 5, "Cinco");
        await CreateAsync(productId, Guid.NewGuid(), 1, "Uno");

        var highest = await _client.GetFromJsonAsync<PageDto>($"/api/reviews/products/{productId}?sort=highest");
        var lowest = await _client.GetFromJsonAsync<PageDto>($"/api/reviews/products/{productId}?sort=lowest");

        highest!.Items.Select(i => i.Rating).Should().Equal(5, 3, 1);
        lowest!.Items.Select(i => i.Rating).Should().Equal(1, 3, 5);
    }

    [Fact]
    public async Task Listar_DeberiaPaginar()
    {
        var productId = Guid.NewGuid();
        for (var i = 0; i < 3; i++) await CreateAsync(productId, Guid.NewGuid(), 4, $"Reseña {i}");

        var page = await _client.GetFromJsonAsync<PageDto>($"/api/reviews/products/{productId}?page=1&pageSize=2");

        page!.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3);
        page.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Listar_ConOrdenInvalido_DeberiaDevolver400()
    {
        (await _client.GetAsync($"/api/reviews/products/{Guid.NewGuid()}?sort=al-azar")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Resúmenes ----
    [Fact]
    public async Task Resumen_DeberiaCalcularPromedioConteoYDistribucion()
    {
        var productId = Guid.NewGuid();
        await CreateAsync(productId, Guid.NewGuid(), 5, "Cinco");
        await CreateAsync(productId, Guid.NewGuid(), 3, "Tres");

        var summary = await _client.GetFromJsonAsync<SummaryDto>($"/api/reviews/products/{productId}/summary");

        summary!.Count.Should().Be(2);
        summary.Average.Should().Be(4.0);
        summary.Distribution[5].Should().Be(1);
        summary.Distribution[3].Should().Be(1);
        summary.Distribution[4].Should().Be(0);
    }

    [Fact]
    public async Task Resumen_DeUnProductoSinReseñas_DeberiaDarCero()
    {
        var summary = await _client.GetFromJsonAsync<SummaryDto>($"/api/reviews/products/{Guid.NewGuid()}/summary");

        summary!.Count.Should().Be(0);
        summary.Average.Should().Be(0);
    }

    [Fact]
    public async Task ResumenesEnLote_DeberiaIncluirTambienLosProductosSinReseñas()
    {
        var conReseñas = Guid.NewGuid();
        var sinReseñas = Guid.NewGuid();
        await CreateAsync(conReseñas, Guid.NewGuid(), 4, "Cuatro");

        // El "!" va en la asignación: así el compilador sabe que la lista no es null en las líneas de abajo.
        var summaries = (await _client.GetFromJsonAsync<List<SummaryDto>>(
            $"/api/reviews/summaries?productIds={conReseñas}&productIds={sinReseñas}"))!;

        summaries.Should().HaveCount(2);
        summaries.Single(s => s.ProductId == conReseñas).Count.Should().Be(1);
        summaries.Single(s => s.ProductId == sinReseñas).Count.Should().Be(0);
    }

    // ---- Mi reseña ----
    [Fact]
    public async Task MiReseña_AntesDeEscribirla_DeberiaDevolver404_YDespuesLaDevuelve()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = TokenFor(userId);

        var before = await _client.SendAsync(Request(HttpMethod.Get, $"/api/reviews/products/{productId}/mine", token));
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await CreateAsync(productId, userId, 4, "Mía");

        var after = await _client.SendAsync(Request(HttpMethod.Get, $"/api/reviews/products/{productId}/mine", token));
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        (await after.Content.ReadFromJsonAsync<ReviewDto>())!.Title.Should().Be("Mía");
    }

    [Fact]
    public async Task MiReseña_SinToken_DeberiaDevolver401()
    {
        (await _client.GetAsync($"/api/reviews/products/{Guid.NewGuid()}/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- Editar ----
    [Fact]
    public async Task Editar_LaPropia_DeberiaActualizarla()
    {
        var userId = Guid.NewGuid();
        var created = await CreateAsync(Guid.NewGuid(), userId, 2, "Regular");

        var response = await _client.SendAsync(Request(HttpMethod.Put, $"/api/reviews/{created.Id}", TokenFor(userId),
            new { rating = 5, title = "Mejoró", comment = "Ahora sí" }));
        var updated = await response.Content.ReadFromJsonAsync<ReviewDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated!.Rating.Should().Be(5);
        updated.Title.Should().Be("Mejoró");
    }

    [Fact]
    public async Task Editar_LaAjena_DeberiaDevolver403()
    {
        var created = await CreateAsync(Guid.NewGuid(), Guid.NewGuid());

        var response = await _client.SendAsync(Request(HttpMethod.Put, $"/api/reviews/{created.Id}", TokenFor(Guid.NewGuid()),
            new { rating = 1, title = "Sabotaje", comment = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editar_LaInexistente_DeberiaDevolver404()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Put, $"/api/reviews/{Guid.NewGuid()}", TokenFor(Guid.NewGuid()),
            new { rating = 3, title = "t", comment = "c" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Eliminar ----
    [Fact]
    public async Task Eliminar_LaPropia_DeberiaDevolver204_YDesaparecerDeLaLista()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var created = await CreateAsync(productId, userId);

        var response = await _client.SendAsync(Request(HttpMethod.Delete, $"/api/reviews/{created.Id}", TokenFor(userId)));
        var page = await _client.GetFromJsonAsync<PageDto>($"/api/reviews/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        page!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Eliminar_LaAjena_DeberiaDevolver403()
    {
        var created = await CreateAsync(Guid.NewGuid(), Guid.NewGuid());

        var response = await _client.SendAsync(Request(HttpMethod.Delete, $"/api/reviews/{created.Id}", TokenFor(Guid.NewGuid())));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Eliminar_ComoAdmin_DeberiaPermitirModerarCualquierReseña()
    {
        var created = await CreateAsync(Guid.NewGuid(), Guid.NewGuid());

        var response = await _client.SendAsync(Request(HttpMethod.Delete, $"/api/reviews/{created.Id}", TokenFor(Guid.NewGuid(), "Admin")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- Compra verificada (evento OrderPaid) ----
    [Fact]
    public async Task EventoOrderPaid_DeberiaMarcarComoVerificadaLaReseñaCreadaDespuesDeComprar()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await PublishOrderPaidAsync(userId, productId);
        (await WaitUntilAsync(() => HasPurchasedAsync(userId, productId))).Should().BeTrue("el consumidor debería registrar la compra");

        var review = await CreateAsync(productId, userId);

        review.IsVerifiedPurchase.Should().BeTrue();
    }

    [Fact]
    public async Task EventoOrderPaid_DeberiaVerificarUnaReseñaEscritaAntesDeComprar()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var review = await CreateAsync(productId, userId);
        review.IsVerifiedPurchase.Should().BeFalse();

        await PublishOrderPaidAsync(userId, productId);

        var verified = await WaitUntilAsync(async () =>
        {
            var mine = await _client.SendAsync(Request(HttpMethod.Get, $"/api/reviews/products/{productId}/mine", TokenFor(userId)));
            return (await mine.Content.ReadFromJsonAsync<ReviewDto>())!.IsVerifiedPurchase;
        });
        verified.Should().BeTrue();
    }

    [Fact]
    public async Task EventoOrderPaid_ConOtroProducto_NoDeberiaVerificarLaReseña()
    {
        var comprado = Guid.NewGuid();
        var reseñado = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await PublishOrderPaidAsync(userId, comprado);
        (await WaitUntilAsync(() => HasPurchasedAsync(userId, comprado))).Should().BeTrue();

        var review = await CreateAsync(reseñado, userId);

        review.IsVerifiedPurchase.Should().BeFalse("compró un producto distinto al que reseña");
    }

    [Fact]
    public async Task EventoOrderPaid_SinProductIds_NoDeberiaFallar_EventosViejosCompatibles()
    {
        var userId = Guid.NewGuid();

        await PublishOrderPaidAsync(userId, productIds: null);
        await Task.Delay(1500);

        (await _client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Convención de colas ----
    [Fact]
    public void LasColasDeEsteServicioLlevanPrefijo_ParaNoCompetirConNotificacionesPorElMismoEvento()
    {
        // Si Reseñas y Notificaciones usaran el mismo nombre de cola ("OrderPaid"), RabbitMQ repartiría
        // cada evento a UNO solo de los dos en vez de entregárselo a ambos.
        var queue = MessagingConventions.EndpointNameFormatter.Consumer<OrderPaidConsumer>();

        queue.Should().StartWith("reviews-");
    }
}

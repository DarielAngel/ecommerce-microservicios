using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Promotions.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.Promotions.IntegrationTests;

public class PromotionsApiTests : IClassFixture<PromotionsApiFactory>
{
    private readonly PromotionsApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly string AdminToken = PromotionsApiFactory.CreateToken(Guid.NewGuid(), "Admin");

    public PromotionsApiTests(PromotionsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record CouponDto(Guid Id, string Code, string Description, string Type, decimal Value, decimal? MaxDiscountAmount,
        decimal MinimumSubtotal, DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, bool OncePerCustomer,
        bool IsActive, int TimesUsed, int ActiveReservations);

    private record QuoteDto(string Code, string Description, decimal Subtotal, decimal DiscountAmount, decimal Total);
    private record RedemptionDto(Guid OrderId, string Code, decimal Subtotal, decimal DiscountAmount, string Status);
    private record ErrorDto(string Message);

    // ---- Helpers ----

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    private static string CustomerToken() => PromotionsApiFactory.CreateToken(Guid.NewGuid());

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private async Task<CouponDto> CreateCouponAsync(string code, string type = "Percentage", decimal value = 10,
        decimal minimum = 0, int? limit = null, bool oncePerCustomer = false, decimal? max = null,
        DateTime? starts = null, DateTime? ends = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/coupons", AdminToken, new
        {
            code, description = $"Cupón {code}", type, value, maxDiscountAmount = max, minimumSubtotal = minimum,
            startsAtUtc = starts, endsAtUtc = ends, usageLimit = limit, oncePerCustomer
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CouponDto>())!;
    }

    private Task<HttpResponseMessage> ValidateAsync(string code, decimal subtotal, string token) =>
        SendAsync(HttpMethod.Get,
            $"/api/coupons/validate?code={Uri.EscapeDataString(code)}&subtotal={subtotal.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            token);

    private Task<HttpResponseMessage> ReserveAsync(Guid orderId, string code, decimal subtotal, string token) =>
        SendAsync(HttpMethod.Post, "/internal/redemptions", token, new { orderId, code, subtotal });

    private async Task<CouponDto> GetCouponAsync(string code)
    {
        var list = await (await SendAsync(HttpMethod.Get, "/api/coupons", AdminToken)).Content.ReadFromJsonAsync<List<CouponDto>>();
        return list!.Single(c => c.Code == code);
    }

    private static async Task<string> MessageOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Message;

    // ---- Salud y seguridad ----

    [Fact]
    public async Task Health_DeberiaResponderOk() =>
        (await _client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

    [Theory]
    [InlineData("GET", "/api/coupons/validate?code=X&subtotal=10")]
    [InlineData("POST", "/internal/redemptions")]
    [InlineData("GET", "/api/coupons")]
    public async Task SinToken_DeberiaResponder401(string method, string url) =>
        (await SendAsync(new HttpMethod(method), url, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task UnCliente_NoPuedeListarNiCrearCupones()
    {
        var token = CustomerToken();
        (await SendAsync(HttpMethod.Get, "/api/coupons", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, "/api/coupons", token,
            new { code = "HACK50", description = "x", type = "Percentage", value = 50 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Administración ----

    [Fact]
    public async Task Admin_CreaUnCupon_YApareceEnLaListaConTipoComoTexto()
    {
        var code = Unique("BIENV");
        await CreateCouponAsync(code.ToLowerInvariant(), type: "FixedAmount", value: 5, minimum: 20);

        var coupon = await GetCouponAsync(code);
        coupon.Type.Should().Be("FixedAmount");
        coupon.Value.Should().Be(5);
        coupon.MinimumSubtotal.Should().Be(20);
        coupon.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_CodigoRepetido_DeberiaResponder409()
    {
        var code = Unique("DUP");
        await CreateCouponAsync(code);

        var response = await SendAsync(HttpMethod.Post, "/api/coupons", AdminToken,
            new { code = code.ToLowerInvariant(), description = "otro", type = "Percentage", value = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_DatosInvalidos_DeberiaResponder400ConElMotivo()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/coupons", AdminToken,
            new { code = Unique("MAL"), description = "Demasiado", type = "Percentage", value = 150 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Contain("porcentaje");
    }

    [Fact]
    public async Task Admin_DesactivaUnCupon_YYaNoSePuedeUsar()
    {
        var created = await CreateCouponAsync(Unique("PAUSA"));

        var update = await SendAsync(HttpMethod.Put, $"/api/coupons/{created.Id}", AdminToken, new
        {
            description = created.Description, type = "Percentage", value = 10, minimumSubtotal = 0,
            oncePerCustomer = false, isActive = false
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await ValidateAsync(created.Code, 100, CustomerToken());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Contain("ya no está disponible");
    }

    // ---- Validación desde el checkout ----

    [Fact]
    public async Task Validar_DeberiaCalcularElDescuento_ConElCodigoEnMinusculas()
    {
        var code = Unique("PCT");
        await CreateCouponAsync(code, value: 20, max: 15);

        var response = await ValidateAsync(code.ToLowerInvariant(), 100, CustomerToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = (await response.Content.ReadFromJsonAsync<QuoteDto>())!;
        quote.DiscountAmount.Should().Be(15, "20 % de 100 es 20, pero el tope es 15");
        quote.Total.Should().Be(85);
    }

    [Fact]
    public async Task Validar_CodigoInexistente_DeberiaResponder404ConMensajeClaro()
    {
        var response = await ValidateAsync("NOEXISTE123", 50, CustomerToken());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageOf(response)).Should().Contain("no existe");
    }

    [Fact]
    public async Task Validar_BajoLaCompraMinima_DeberiaResponder400()
    {
        var code = Unique("MIN");
        await CreateCouponAsync(code, minimum: 50);

        var response = await ValidateAsync(code, 30, CustomerToken());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Contain("compra mínima");
    }

    [Fact]
    public async Task Validar_CuponVencido_DeberiaResponder400()
    {
        var code = Unique("VIEJO");
        await CreateCouponAsync(code, starts: DateTime.UtcNow.AddDays(-10), ends: DateTime.UtcNow.AddDays(-1));

        var response = await ValidateAsync(code, 100, CustomerToken());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Contain("venció");
    }

    // ---- Canje en la saga: reservar → confirmar / liberar ----

    [Fact]
    public async Task Reservar_YConfirmar_DeberiaContarUnUsoDefinitivo()
    {
        var code = Unique("SAGA");
        await CreateCouponAsync(code, value: 10, limit: 3);
        var token = CustomerToken();
        var orderId = Guid.NewGuid();

        var reserve = await ReserveAsync(orderId, code, 80, token);
        reserve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reserve.Content.ReadFromJsonAsync<RedemptionDto>())!.DiscountAmount.Should().Be(8);
        (await GetCouponAsync(code)).ActiveReservations.Should().Be(1);

        var confirm = await SendAsync(HttpMethod.Post, $"/internal/redemptions/{orderId}/confirm", token);
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var coupon = await GetCouponAsync(code);
        coupon.TimesUsed.Should().Be(1);
        coupon.ActiveReservations.Should().Be(0);
    }

    [Fact]
    public async Task Reservar_YLiberar_DeberiaDevolverElUso()
    {
        var code = Unique("LIB");
        await CreateCouponAsync(code, limit: 1);
        var token = CustomerToken();
        var orderId = Guid.NewGuid();
        await ReserveAsync(orderId, code, 50, token);

        // Con el único uso apartado, otro cliente no puede usarlo...
        var other = CustomerToken();
        (await ValidateAsync(code, 50, other)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // ...hasta que el pago falla y Órdenes libera el uso.
        (await SendAsync(HttpMethod.Post, $"/internal/redemptions/{orderId}/release", token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValidateAsync(code, 50, other)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReservarDosVecesLaMismaOrden_DeberiaContarUnSoloUso()
    {
        var code = Unique("IDEM");
        await CreateCouponAsync(code, limit: 5);
        var token = CustomerToken();
        var orderId = Guid.NewGuid();

        (await ReserveAsync(orderId, code, 50, token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReserveAsync(orderId, code, 50, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetCouponAsync(code)).ActiveReservations.Should().Be(1);
    }

    [Fact]
    public async Task CheckoutsSimultaneos_SobreUnCuponDeUnSoloUso_SoloUnoLoConsigue()
    {
        // La prueba clave del "canje atómico": sin el bloqueo de fila, varios contarían 0 usos a la vez
        // y todos se llevarían el descuento.
        var code = Unique("FLASH");
        await CreateCouponAsync(code, limit: 1);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            ReserveAsync(Guid.NewGuid(), code, 100, CustomerToken())));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest).Should().Be(9);
        (await GetCouponAsync(code)).ActiveReservations.Should().Be(1);
    }

    [Fact]
    public async Task DeUnSoloUsoPorCliente_ElMismoClienteNoPuedeRepetir_PeroOtroSi()
    {
        var code = Unique("UNAVEZ");
        await CreateCouponAsync(code, oncePerCustomer: true);
        var ana = CustomerToken();

        (await ReserveAsync(Guid.NewGuid(), code, 50, ana)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await ReserveAsync(Guid.NewGuid(), code, 50, ana);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(again)).Should().Contain("un solo uso por cliente");

        (await ReserveAsync(Guid.NewGuid(), code, 50, CustomerToken())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnaReservaAbandonadaHaceMasDeDosHoras_YaNoBloqueaElCupon()
    {
        var code = Unique("ABAND");
        await CreateCouponAsync(code, limit: 1);
        var orderId = Guid.NewGuid();
        await ReserveAsync(orderId, code, 50, CustomerToken());

        // Simulamos que el cliente abandonó el pago hace 3 horas.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE coupon_redemptions SET created_at_utc = {DateTime.UtcNow.AddHours(-3)} WHERE order_id = {orderId}");
        }

        (await ValidateAsync(code, 50, CustomerToken())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtroCliente_NoPuedeConfirmarNiLiberarUnCanjeAjeno()
    {
        var code = Unique("AJENO");
        await CreateCouponAsync(code);
        var orderId = Guid.NewGuid();
        await ReserveAsync(orderId, code, 50, CustomerToken());
        var intruder = CustomerToken();

        (await SendAsync(HttpMethod.Post, $"/internal/redemptions/{orderId}/confirm", intruder))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"/internal/redemptions/{orderId}/release", intruder))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LiberarUnaOrdenSinCupon_DeberiaResponder204() =>
        (await SendAsync(HttpMethod.Post, $"/internal/redemptions/{Guid.NewGuid()}/release", CustomerToken()))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
}

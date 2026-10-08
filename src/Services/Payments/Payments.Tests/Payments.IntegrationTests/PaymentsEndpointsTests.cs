using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Ecommerce.Payments.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Payments.IntegrationTests;

public class PaymentsEndpointsTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;
    private readonly HttpClient _client;

    public PaymentsEndpointsTests(PaymentsApiFactory factory)
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

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePayment_SinToken_DeberiaDevolver401()
    {
        var response = await _client.PostAsJsonAsync("/api/payments", new { OrderId = Guid.NewGuid(), Amount = 10m, Currency = "USD" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePayment_ConDatosValidos_DeberiaDevolverUrlDeAprobacion()
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        var request = WithAuth(HttpMethod.Post, "/api/payments", token);
        request.Content = JsonContent.Create(new { OrderId = orderId, Amount = 49.99m, Currency = "USD" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaymentResult>();
        result!.Status.Should().Be("PendingApproval");
        result.ApproveUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreatePayment_LlamadoDosVecesParaLaMismaOrden_DeberiaSerIdempotente()
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        var firstRequest = WithAuth(HttpMethod.Post, "/api/payments", token);
        firstRequest.Content = JsonContent.Create(new { OrderId = orderId, Amount = 20m, Currency = "USD" });
        var firstResponse = await _client.SendAsync(firstRequest);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<PaymentResult>();

        var secondRequest = WithAuth(HttpMethod.Post, "/api/payments", token);
        secondRequest.Content = JsonContent.Create(new { OrderId = orderId, Amount = 20m, Currency = "USD" });
        var secondResponse = await _client.SendAsync(secondRequest);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<PaymentResult>();

        secondResult!.PaymentId.Should().Be(firstResult!.PaymentId, "no debería crearse un segundo pago para la misma orden");
    }

    [Fact]
    public async Task FlujoCompleto_CrearYCapturar_DeberiaQuedarCaptured()
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        var createRequest = WithAuth(HttpMethod.Post, "/api/payments", token);
        createRequest.Content = JsonContent.Create(new { OrderId = orderId, Amount = 30m, Currency = "USD" });
        await _client.SendAsync(createRequest);

        var captureResponse = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/payments/{orderId}/capture", token));
        var captureResult = await captureResponse.Content.ReadFromJsonAsync<PaymentResult>();

        captureResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        captureResult!.Status.Should().Be("Captured");
        captureResult.PayPalCaptureId.Should().NotBeNullOrWhiteSpace();

        var getResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}", token));
        var getResult = await getResponse.Content.ReadFromJsonAsync<PaymentResult>();
        getResult!.Status.Should().Be("Captured");
    }

    [Fact]
    public async Task Capture_CuandoPayPalRechaza_DeberiaDevolver409YMarcarFallido()
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        var createRequest = WithAuth(HttpMethod.Post, "/api/payments", token);
        createRequest.Content = JsonContent.Create(new { OrderId = orderId, Amount = 15m, Currency = "USD" });
        var createResponse = await _client.SendAsync(createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<PaymentResult>();

        // Extraemos el PayPalOrderId a partir del approveUrl fake para configurar el fallo.
        var payPalOrderId = $"FAKE-PP-{orderId:N}";
        _factory.FakePayPal.OrdersThatShouldFailCapture.Add(payPalOrderId);

        var captureResponse = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/payments/{orderId}/capture", token));

        captureResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var getResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}", token));
        var getResult = await getResponse.Content.ReadFromJsonAsync<PaymentResult>();
        getResult!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetPayment_SinPagoParaLaOrden_DeberiaDevolver404()
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{Guid.NewGuid()}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_ConFirmaValida_DeberiaResponder200()
    {
        var payload = """{"event_type":"PAYMENT.CAPTURE.COMPLETED","resource":{"id":"X"}}""";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Reembolsos (Fase 7) ----

    private async Task<Guid> CapturedOrderAsync(decimal amount)
    {
        var token = PaymentsApiFactory.CreateToken(Guid.NewGuid());
        var orderId = Guid.NewGuid();
        var create = WithAuth(HttpMethod.Post, "/api/payments", token);
        create.Content = JsonContent.Create(new { OrderId = orderId, Amount = amount, Currency = "USD" });
        (await _client.SendAsync(create)).EnsureSuccessStatusCode();
        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/payments/{orderId}/capture", token))).EnsureSuccessStatusCode();
        return orderId;
    }

    private Task<HttpResponseMessage> RefundAsync(Guid orderId, Guid refundId, decimal amount, string role = "Admin")
    {
        var request = WithAuth(HttpMethod.Post, $"/api/payments/{orderId}/refunds", PaymentsApiFactory.CreateToken(Guid.NewGuid(), role));
        request.Content = JsonContent.Create(new { RefundId = refundId, Amount = amount, Reason = "Devolución de prueba" });
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Reembolso_SoloLoPuedeHacerUnAdmin()
    {
        var orderId = await CapturedOrderAsync(30m);

        (await RefundAsync(orderId, Guid.NewGuid(), 10m, role: "Cliente")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reembolsos_ParcialesHastaElTotal_QuedanGuardadosYElPagoTerminaRefunded()
    {
        var orderId = await CapturedOrderAsync(30m);

        var first = await RefundAsync(orderId, Guid.NewGuid(), 12.5m);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<RefundResult>())!.Should().Match<RefundResult>(r =>
            r.TotalRefunded == 12.5m && r.Refundable == 17.5m && r.PaymentStatus == "Captured");

        var tooMuch = await RefundAsync(orderId, Guid.NewGuid(), 17.51m);
        tooMuch.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var last = await (await RefundAsync(orderId, Guid.NewGuid(), 17.5m)).Content.ReadFromJsonAsync<RefundResult>();
        last!.PaymentStatus.Should().Be("Refunded");
        last.Refundable.Should().Be(0m);

        // Quedó guardado en la base (no solo en memoria): una consulta nueva ve el pago reembolsado.
        var get = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}", PaymentsApiFactory.CreateToken(Guid.NewGuid())));
        (await get.Content.ReadFromJsonAsync<PaymentResult>())!.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task Reembolso_RepetidoConLaMismaClave_NoDevuelveDosVeces()
    {
        var orderId = await CapturedOrderAsync(20m);
        var refundId = Guid.NewGuid();

        var first = await (await RefundAsync(orderId, refundId, 8m)).Content.ReadFromJsonAsync<RefundResult>();
        var again = await RefundAsync(orderId, refundId, 8m);

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await again.Content.ReadFromJsonAsync<RefundResult>();
        second!.PayPalRefundId.Should().Be(first!.PayPalRefundId);
        second.TotalRefunded.Should().Be(8m, "el segundo pedido con la misma clave no suma otro reembolso");
    }

    [Fact]
    public async Task Reembolso_SiPayPalLoRechaza_Devuelve409YNoQuedaRegistrado()
    {
        var orderId = await CapturedOrderAsync(20m);
        _factory.FakePayPal.FailRefunds = true;
        try
        {
            (await RefundAsync(orderId, Guid.NewGuid(), 5m)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            _factory.FakePayPal.FailRefunds = false;
        }

        var ok = await (await RefundAsync(orderId, Guid.NewGuid(), 20m)).Content.ReadFromJsonAsync<RefundResult>();
        ok!.TotalRefunded.Should().Be(20m, "el intento rechazado no descontó nada");
    }

    [Fact]
    public async Task Reembolso_DeUnaOrdenSinPago_Devuelve404()
    {
        (await RefundAsync(Guid.NewGuid(), Guid.NewGuid(), 5m)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsultarReembolso_DevuelveElHecho_Y404SiNunca()
    {
        var orderId = await CapturedOrderAsync(20m);
        var refundId = Guid.NewGuid();
        await RefundAsync(orderId, refundId, 5m);
        var admin = PaymentsApiFactory.CreateToken(Guid.NewGuid(), "Admin");

        var found = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}/refunds/{refundId}", admin));
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<RefundResult>())!.Amount.Should().Be(5m);

        (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}/refunds/{Guid.NewGuid()}", admin)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/payments/{orderId}/refunds/{refundId}", PaymentsApiFactory.CreateToken(Guid.NewGuid()))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CapturarDeNuevoUnPagoReembolsado_NoLoMarcaFallido()
    {
        var orderId = await CapturedOrderAsync(10m);
        await RefundAsync(orderId, Guid.NewGuid(), 10m);

        var again = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/payments/{orderId}/capture", PaymentsApiFactory.CreateToken(Guid.NewGuid())));

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<PaymentResult>())!.Status.Should().Be("Refunded");
    }
}

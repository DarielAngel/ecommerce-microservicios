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
}

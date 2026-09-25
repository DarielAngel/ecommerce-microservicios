using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Orders.IntegrationTests;

public class OrdersEndpointsTests : IClassFixture<OrdersApiFactory>
{
    private readonly OrdersApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersEndpointsTests(OrdersApiFactory factory)
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

    private static CartItemInfo BuildCartItem(Guid variantId, int quantity = 2) =>
        new(variantId, Guid.NewGuid(), "Camiseta", "SKU-1", 20m, quantity);

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_SinToken_DeberiaDevolver401()
    {
        var response = await _client.PostAsJsonAsync("/api/orders/checkout", new { VariantIds = new List<Guid>(), ShippingAddress = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_ConStockYPagoOk_DeberiaCrearLaOrdenPendingPayment()
    {
        var userId = Guid.NewGuid();
        var token = OrdersApiFactory.CreateToken(userId);
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;

        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>();
        result!.Status.Should().Be("PendingPayment");
        result.TotalAmount.Should().Be(40m);
        result.ApproveUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Checkout_SinStockSuficiente_DeberiaDevolver409()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = false;

        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Resetear para no afectar otros tests que comparten el fixture.
        _factory.FakeInventory.ShouldReserveSucceed = true;
    }

    [Fact]
    public async Task FlujoCompleto_CheckoutYConfirmarConCapturaExitosa_DeberiaQuedarPaidYLimpiarElCarrito()
    {
        var userId = Guid.NewGuid();
        var token = OrdersApiFactory.CreateToken(userId);
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = true;

        var checkoutRequest = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        checkoutRequest.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var checkoutResponse = await _client.SendAsync(checkoutRequest);
        var checkoutResult = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResult>();

        var confirmResponse = await _client.SendAsync(
            WithAuth(HttpMethod.Post, $"/api/orders/{checkoutResult!.OrderId}/confirm-payment", token));
        var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<CheckoutResult>();

        confirmResult!.Status.Should().Be("Paid");
        _factory.FakeInventory.ConfirmedOrders.Should().ContainKey(checkoutResult.OrderId);
        _factory.FakeCart.RemovedVariantIds.Should().Contain(variantId);
    }

    [Fact]
    public async Task FlujoCompleto_CheckoutYConfirmarConCapturaFallida_DeberiaQuedarFailedYLiberarStock()
    {
        var userId = Guid.NewGuid();
        var token = OrdersApiFactory.CreateToken(userId);
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = false;

        var checkoutRequest = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        checkoutRequest.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var checkoutResponse = await _client.SendAsync(checkoutRequest);
        var checkoutResult = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResult>();

        var confirmResponse = await _client.SendAsync(
            WithAuth(HttpMethod.Post, $"/api/orders/{checkoutResult!.OrderId}/confirm-payment", token));
        var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<CheckoutResult>();

        confirmResult!.Status.Should().Be("Failed");
        _factory.FakeInventory.ReleasedOrders.Should().ContainKey(checkoutResult.OrderId);

        _factory.FakePayments.ShouldCaptureSucceed = true; // resetear para otros tests
    }

    [Fact]
    public async Task GetById_DeOtroUsuario_DeberiaDevolver404()
    {
        var ownerToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var otherUserToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;

        var checkoutRequest = WithAuth(HttpMethod.Post, "/api/orders/checkout", ownerToken);
        checkoutRequest.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var checkoutResponse = await _client.SendAsync(checkoutRequest);
        var checkoutResult = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResult>();

        var response = await _client.SendAsync(
            WithAuth(HttpMethod.Get, $"/api/orders/{checkoutResult!.OrderId}", otherUserToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMine_DeberiaDevolverSoloLasOrdenesDelUsuarioAutenticado()
    {
        var userId = Guid.NewGuid();
        var token = OrdersApiFactory.CreateToken(userId);
        var variantId = Guid.NewGuid();

        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;

        var checkoutRequest = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        checkoutRequest.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        await _client.SendAsync(checkoutRequest);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders", token));
        var orders = await response.Content.ReadFromJsonAsync<List<CheckoutResult>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        orders.Should().NotBeEmpty();
    }
}

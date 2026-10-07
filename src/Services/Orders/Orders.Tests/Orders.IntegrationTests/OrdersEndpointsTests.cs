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

    [Fact]
    public async Task Checkout_ConTokenSinEmail_DeberiaDevolver401NoUn500()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid(), includeEmail: false);

        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new { VariantIds = new List<Guid> { Guid.NewGuid() }, ShippingAddress = "Calle Falsa 123" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> CreatePaidOrderAsync(string userToken)
    {
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = true;

        var checkout = WithAuth(HttpMethod.Post, "/api/orders/checkout", userToken);
        checkout.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var checkoutResult = await (await _client.SendAsync(checkout)).Content.ReadFromJsonAsync<CheckoutResult>();

        await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{checkoutResult!.OrderId}/confirm-payment", userToken));
        return checkoutResult.OrderId;
    }

    [Fact]
    public async Task Ship_ConRolAdminSobreOrdenPagada_DeberiaQuedarShipped()
    {
        var orderId = await CreatePaidOrderAsync(OrdersApiFactory.CreateToken(Guid.NewGuid()));
        var adminToken = OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin");

        var response = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", adminToken));
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task Ship_LlamadoDosVeces_DeberiaSerIdempotente()
    {
        var orderId = await CreatePaidOrderAsync(OrdersApiFactory.CreateToken(Guid.NewGuid()));
        var adminToken = OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin");

        await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", adminToken));
        var second = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", adminToken));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ship_ConRolCliente_DeberiaDevolver403()
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var orderId = await CreatePaidOrderAsync(userToken);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", userToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ship_SobreOrdenSinPagar_DeberiaDevolver400()
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;

        var checkout = WithAuth(HttpMethod.Post, "/api/orders/checkout", userToken);
        checkout.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var checkoutResult = await (await _client.SendAsync(checkout)).Content.ReadFromJsonAsync<CheckoutResult>();

        var response = await _client.SendAsync(WithAuth(
            HttpMethod.Post, $"/api/orders/{checkoutResult!.OrderId}/ship", OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListAll_ConRolCliente_DeberiaDevolver403()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/all", token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListAll_ConRolAdmin_DeberiaIncluirOrdenesDeDistintosUsuarios()
    {
        var user1Token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var user2Token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var adminToken = OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin");

        foreach (var token in new[] { user1Token, user2Token })
        {
            var variantId = Guid.NewGuid();
            _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
            _factory.FakeInventory.ShouldReserveSucceed = true;

            var checkout = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
            checkout.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
            await _client.SendAsync(checkout);
        }

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/all?count=500", adminToken));
        var orders = await response.Content.ReadFromJsonAsync<List<AdminOrderResult>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        orders!.Select(o => o.UserId).Distinct().Count().Should().BeGreaterThanOrEqualTo(2);
        orders.Should().OnlyContain(o => !string.IsNullOrEmpty(o.UserEmail));
    }

    // ---- Cupones (Fase 3) ----

    private async Task<HttpResponseMessage> CheckoutAsync(string token, Guid variantId, string? couponCode)
    {
        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new
        {
            VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123", CouponCode = couponCode
        });
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Checkout_ConCupon_GuardaElDescuentoYLoMuestraAlConsultarLaOrden()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) }; // 2 × $20
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakeCoupons.ValidCoupons["INTEG5"] = 5m;

        var response = await CheckoutAsync(token, variantId, "integ5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        checkout.Subtotal.Should().Be(40m);
        checkout.DiscountAmount.Should().Be(5m);
        checkout.TotalAmount.Should().Be(35m);
        checkout.CouponCode.Should().Be("INTEG5");
        _factory.FakeCoupons.ReservedSubtotals[checkout.OrderId].Should().Be(40m);

        // Persistido: al volver a leer la orden de la base, el descuento sigue ahí.
        var stored = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{checkout.OrderId}", token));
        var order = (await stored.Content.ReadFromJsonAsync<CheckoutResult>())!;
        order.TotalAmount.Should().Be(35m);
        order.CouponCode.Should().Be("INTEG5");
    }

    [Fact]
    public async Task Checkout_ConCuponInvalido_Devuelve409ConElMotivoYLiberaElStock()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        var releasedBefore = _factory.FakeInventory.ReleasedOrders.Count;

        var response = await CheckoutAsync(token, variantId, "NOEXISTE");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("no existe");
        _factory.FakeInventory.ReleasedOrders.Count.Should().Be(releasedBefore + 1);
    }

    [Fact]
    public async Task ConfirmarPago_DeUnaOrdenConCupon_ConfirmaElUsoDelCupon()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = true;
        _factory.FakeCoupons.ValidCoupons["PAGADO10"] = 10m;

        var checkout = (await (await CheckoutAsync(token, variantId, "PAGADO10")).Content.ReadFromJsonAsync<CheckoutResult>())!;
        var confirm = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{checkout.OrderId}/confirm-payment", token));

        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await confirm.Content.ReadFromJsonAsync<CheckoutResult>())!.Status.Should().Be("Paid");
        _factory.FakeCoupons.ConfirmedOrders.Should().ContainKey(checkout.OrderId);
    }
}

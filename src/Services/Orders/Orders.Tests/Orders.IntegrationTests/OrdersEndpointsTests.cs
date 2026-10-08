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
    public async Task LineaDeTiempo_PagadaYLuegoEnviada_TraeFechasYEntregaEstimada()
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var orderId = await CreatePaidOrderAsync(userToken);

        var paid = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken));
        var paidJson = await paid.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        paidJson.GetProperty("status").GetString().Should().Be("Paid");
        paidJson.GetProperty("shippingAddress").GetString().Should().Be("Calle Falsa 123");
        paidJson.GetProperty("paidAtUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        paidJson.GetProperty("shippedAtUtc").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        // Las fechas estimadas viajan como "aaaa-mm-dd" (días, no instantes).
        paidJson.GetProperty("estimatedDeliveryFrom").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
        paidJson.GetProperty("lines")[0].GetProperty("productId").GetGuid().Should().NotBeEmpty();

        var adminToken = OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin");
        await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", adminToken));

        var shipped = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken)))
            .Content.ReadFromJsonAsync<CheckoutResult>();
        shipped!.Status.Should().Be("Shipped");
        shipped.ShippedAtUtc.Should().NotBeNull();
        shipped.EstimatedDeliveryFrom.Should().Be(
            Ecommerce.Orders.Domain.Entities.DeliveryEstimate.AddBusinessDays(shipped.ShippedAtUtc!.Value, 1));
        shipped.EstimatedDeliveryTo.Should().BeOnOrAfter(shipped.EstimatedDeliveryFrom!.Value);
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

    // ---- Puntos (Fase 6) ----

    [Fact]
    public async Task Checkout_UsandoPuntos_DescuentaYConfirmaElCanjeAlPagar()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) }; // $40
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = true;
        _factory.FakeCoupons.ValidCoupons["MENOS10"] = 10m;
        _factory.FakeLoyalty.Balance = 900;

        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new
        {
            VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123", CouponCode = "MENOS10", UsePoints = true
        });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        // $40 − $10 del cupón = $30; con puntos como mucho la mitad: $15 (1500 pts), pero hay 900 → $9.
        _factory.FakeLoyalty.ReservedAmounts[checkout.OrderId].Should().Be(30m);
        checkout.LoyaltyPoints.Should().Be(900);
        checkout.LoyaltyDiscount.Should().Be(9m);
        checkout.TotalAmount.Should().Be(21m);

        var confirm = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{checkout.OrderId}/confirm-payment", token));
        var paid = (await confirm.Content.ReadFromJsonAsync<CheckoutResult>())!;
        paid.Status.Should().Be("Paid");
        paid.LoyaltyDiscount.Should().Be(9m, "persistido en la base");
        _factory.FakeLoyalty.ConfirmedOrders.Should().ContainKey(checkout.OrderId);
    }

    [Fact]
    public async Task Checkout_SinPuntosSuficientes_Devuelve409ConElMotivoYLiberaElStock()
    {
        var token = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakeLoyalty.Balance = 40;
        var releasedBefore = _factory.FakeInventory.ReleasedOrders.Count;

        var request = WithAuth(HttpMethod.Post, "/api/orders/checkout", token);
        request.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle 1", UsePoints = true });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("message").GetString()
            .Should().Be("Necesitas al menos 100 puntos para usarlos (tienes 40).");
        _factory.FakeInventory.ReleasedOrders.Count.Should().Be(releasedBefore + 1);
    }

    // ---- Devoluciones (Fase 7) ----

    private async Task<(Guid OrderId, Guid VariantId, string UserToken)> ShippedOrderAsync(int quantity = 2)
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId, quantity) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        _factory.FakePayments.ShouldCaptureSucceed = true;
        var checkout = WithAuth(HttpMethod.Post, "/api/orders/checkout", userToken);
        checkout.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var order = await (await _client.SendAsync(checkout)).Content.ReadFromJsonAsync<CheckoutResult>();
        await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{order!.OrderId}/confirm-payment", userToken));
        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{order.OrderId}/ship", OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin"))))
            .EnsureSuccessStatusCode();
        return (order.OrderId, variantId, userToken);
    }

    private Task<HttpResponseMessage> RequestReturnAsync(Guid orderId, string token, Guid variantId, int quantity, string reason = "Damaged")
    {
        var request = WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/returns", token);
        request.Content = JsonContent.Create(new { Items = new[] { new { VariantId = variantId, Quantity = quantity } }, Reason = reason, Comment = "Llegó roto" });
        return _client.SendAsync(request);
    }

    private static string AdminToken() => OrdersApiFactory.CreateToken(Guid.NewGuid(), "Admin");

    [Fact]
    public async Task Devolucion_FlujoCompleto_PedirAprobarYReembolsar()
    {
        var (orderId, variantId, userToken) = await ShippedOrderAsync(quantity: 2);

        // El cliente pide devolver 1 de 2.
        var requested = await RequestReturnAsync(orderId, userToken, variantId, 1);
        requested.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await requested.Content.ReadFromJsonAsync<CheckoutResult>();
        var ret = order!.Returns!.Should().ContainSingle().Subject;
        ret.Status.Should().Be("Requested");
        order.Lines.Single().ReturnableQuantity.Should().Be(1);
        order.CanRequestReturn.Should().BeFalse();

        // El Admin la ve en "pendientes" con lo que se va a devolver.
        var pending = await (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/returns?status=Requested", AdminToken())))
            .Content.ReadFromJsonAsync<List<AdminReturnResult>>();
        var listed = pending!.Single(r => r.ReturnId == ret.ReturnId);
        listed.RefundAmount.Should().Be(order.TotalAmount / 2);
        listed.UserEmail.Should().NotBeNullOrWhiteSpace();

        // Aprueba: se reembolsa en Pagos y se publica el evento para Inventario, Lealtad y Notificaciones.
        var approved = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/approve", AdminToken()));
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approved.Content.ReadFromJsonAsync<AdminReturnResult>())!.Status.Should().Be("Refunded");
        _factory.FakePayments.Refunds[ret.ReturnId].Should().Be(order.TotalAmount / 2);
        _factory.Published.Events.OfType<Ecommerce.Contracts.Events.OrderRefundedEvent>()
            .Should().ContainSingle(e => e.ReturnId == ret.ReturnId).Which.Items.Single().Should()
            .Match<Ecommerce.Contracts.Events.RefundedItem>(i => i.VariantId == variantId && i.Quantity == 1);

        // Quedó guardado: el cliente ve su devolución reembolsada y lo devuelto en dinero.
        var reloaded = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken)))
            .Content.ReadFromJsonAsync<CheckoutResult>();
        reloaded!.Status.Should().Be("Shipped");
        reloaded.RefundedAmount.Should().Be(order.TotalAmount / 2);
        reloaded.Returns!.Single().Status.Should().Be("Refunded");
        reloaded.CanRequestReturn.Should().BeTrue("todavía queda 1 unidad por devolver");

        // Devuelve la otra: el pedido queda reembolsado completo.
        var second = (await (await RequestReturnAsync(orderId, userToken, variantId, 1)).Content.ReadFromJsonAsync<CheckoutResult>())!
            .Returns!.First(r => r.Status == "Requested");
        await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{second.ReturnId}/approve", AdminToken()));
        var final = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken)))
            .Content.ReadFromJsonAsync<CheckoutResult>();
        final!.Status.Should().Be("Refunded");
        final.RefundedAmount.Should().Be(final.TotalAmount);
    }

    [Fact]
    public async Task Devolucion_SiPagosRechaza_QuedaAprobadaYSePuedeReintentar()
    {
        var (orderId, variantId, userToken) = await ShippedOrderAsync();
        var ret = (await (await RequestReturnAsync(orderId, userToken, variantId, 2)).Content.ReadFromJsonAsync<CheckoutResult>())!.Returns!.Single();

        _factory.FakePayments.RefundFailure = "PayPal rechazó el reembolso (422).";
        try
        {
            var failed = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/approve", AdminToken()));
            failed.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await failed.Content.ReadAsStringAsync()).Should().Contain("422");
        }
        finally
        {
            _factory.FakePayments.RefundFailure = null;
        }

        var approved = await (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/returns?status=Approved", AdminToken())))
            .Content.ReadFromJsonAsync<List<AdminReturnResult>>();
        approved!.Should().Contain(r => r.ReturnId == ret.ReturnId, "el monto quedó fijado aunque PayPal fallara");

        var retry = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/approve", AdminToken()));
        (await retry.Content.ReadFromJsonAsync<AdminReturnResult>())!.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task Devolucion_AprobadaSinReembolso_SePuedeCancelarYElPedidoNoQuedaTrabado()
    {
        var (orderId, variantId, userToken) = await ShippedOrderAsync();
        var ret = (await (await RequestReturnAsync(orderId, userToken, variantId, 1)).Content.ReadFromJsonAsync<CheckoutResult>())!.Returns!.Single();

        _factory.FakePayments.RefundFailure = "El pago no admite reembolsos.";
        try
        {
            (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/approve", AdminToken())))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            _factory.FakePayments.RefundFailure = null;
        }

        var cancel = WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/reject", AdminToken());
        cancel.Content = JsonContent.Create(new { Note = "PayPal no permite reembolsar este pago; te contactamos." });
        var cancelled = await _client.SendAsync(cancel);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelled.Content.ReadFromJsonAsync<AdminReturnResult>())!.Status.Should().Be("Rejected");

        var order = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken))).Content.ReadFromJsonAsync<CheckoutResult>();
        order!.CanRequestReturn.Should().BeTrue();
        order.RefundedAmount.Should().Be(0);
    }

    [Fact]
    public async Task Devolucion_Rechazada_GuardaLaNotaYLiberaLasUnidades()
    {
        var (orderId, variantId, userToken) = await ShippedOrderAsync();
        var ret = (await (await RequestReturnAsync(orderId, userToken, variantId, 2)).Content.ReadFromJsonAsync<CheckoutResult>())!.Returns!.Single();

        var noNote = WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/reject", AdminToken());
        noNote.Content = JsonContent.Create(new { Note = "" });
        (await _client.SendAsync(noNote)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reject = WithAuth(HttpMethod.Post, $"/api/orders/returns/{ret.ReturnId}/reject", AdminToken());
        reject.Content = JsonContent.Create(new { Note = "El producto tiene uso." });
        (await _client.SendAsync(reject)).StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken))).Content.ReadFromJsonAsync<CheckoutResult>();
        order!.Returns!.Single().AdminNote.Should().Be("El producto tiene uso.");
        order.Lines.Single().ReturnableQuantity.Should().Be(2);
        order.CanRequestReturn.Should().BeTrue();
        _factory.Published.Events.OfType<Ecommerce.Contracts.Events.ReturnRejectedEvent>().Should().Contain(e => e.ReturnId == ret.ReturnId);
    }

    [Fact]
    public async Task Devolucion_ReglasYPermisos()
    {
        var (orderId, variantId, userToken) = await ShippedOrderAsync();

        // Otro cliente no puede pedir devoluciones de este pedido (ni saber que existe).
        (await RequestReturnAsync(orderId, OrdersApiFactory.CreateToken(Guid.NewGuid()), variantId, 1)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Más unidades de las compradas, o un motivo inventado: 400.
        (await RequestReturnAsync(orderId, userToken, variantId, 3)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RequestReturnAsync(orderId, userToken, variantId, 1, reason: "Porque sí")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Un cliente no ve ni aprueba devoluciones.
        (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/returns", userToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{Guid.NewGuid()}/approve", userToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{Guid.NewGuid()}/approve", AdminToken()))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Un pedido pagado pero sin enviar todavía no se puede devolver.
        var paidOnly = await CreatePaidOrderAsync(userToken);
        (await RequestReturnAsync(paidOnly, userToken, _factory.FakeCart.Items[0].VariantId, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Cancelar antes del envío (Fase 7) ----

    private Task<HttpResponseMessage> CancelAsync(Guid orderId, string token, string reason = "ChangedMind")
    {
        var request = WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/cancel", token);
        request.Content = JsonContent.Create(new { Reason = reason, Comment = "Ya no lo necesito" });
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Cancelar_PendienteDePago_LiberaElStockYQuedaCancelada()
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var variantId = Guid.NewGuid();
        _factory.FakeCart.Items = new List<CartItemInfo> { BuildCartItem(variantId) };
        _factory.FakeInventory.ShouldReserveSucceed = true;
        var checkout = WithAuth(HttpMethod.Post, "/api/orders/checkout", userToken);
        checkout.Content = JsonContent.Create(new { VariantIds = new List<Guid> { variantId }, ShippingAddress = "Calle Falsa 123" });
        var order = await (await _client.SendAsync(checkout)).Content.ReadFromJsonAsync<CheckoutResult>();
        order!.CanCancel.Should().BeTrue();

        var cancelled = await CancelAsync(order.OrderId, userToken);

        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancelled.Content.ReadFromJsonAsync<CheckoutResult>();
        result!.Status.Should().Be("Cancelled");
        result.CancelledAtUtc.Should().NotBeNull();
        _factory.FakeInventory.ReleasedOrders.Should().ContainKey(order.OrderId);

        // Confirmar el pago después ya no cobra nada.
        var confirm = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{order.OrderId}/confirm-payment", userToken));
        (await confirm.Content.ReadFromJsonAsync<CheckoutResult>())!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Cancelar_Pagado_ElClienteLaPide_NoSePuedeEnviar_YElAdminLaApruebaConReembolsoTotal()
    {
        var userToken = OrdersApiFactory.CreateToken(Guid.NewGuid());
        var orderId = await CreatePaidOrderAsync(userToken);

        var requested = await (await CancelAsync(orderId, userToken)).Content.ReadFromJsonAsync<CheckoutResult>();
        requested!.Status.Should().Be("Paid");
        requested.CanCancel.Should().BeFalse();
        var cancellation = requested.Returns!.Single();
        cancellation.IsCancellation.Should().BeTrue();

        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/{orderId}/ship", AdminToken())))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var pending = await (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/orders/returns?status=Requested", AdminToken())))
            .Content.ReadFromJsonAsync<List<AdminReturnResult>>();
        pending!.Single(r => r.ReturnId == cancellation.ReturnId).Should().Match<AdminReturnResult>(r => r.IsCancellation && r.CompletesOrder);

        (await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/orders/returns/{cancellation.ReturnId}/approve", AdminToken())))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var final = await (await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/orders/{orderId}", userToken))).Content.ReadFromJsonAsync<CheckoutResult>();
        final!.Status.Should().Be("Cancelled");
        final.RefundedAmount.Should().Be(final.TotalAmount);
        _factory.FakePayments.Refunds[cancellation.ReturnId].Should().Be(final.TotalAmount);
        _factory.Published.Events.OfType<Ecommerce.Contracts.Events.OrderRefundedEvent>()
            .Should().ContainSingle(e => e.ReturnId == cancellation.ReturnId).Which.OrderCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_ElAdminLaCancelaDirectoYSeReembolsaEnElActo()
    {
        var orderId = await CreatePaidOrderAsync(OrdersApiFactory.CreateToken(Guid.NewGuid()));

        var result = await (await CancelAsync(orderId, AdminToken(), reason: "Other")).Content.ReadFromJsonAsync<CheckoutResult>();

        result!.Status.Should().Be("Cancelled");
        result.RefundedAmount.Should().Be(result.TotalAmount);
    }

    [Fact]
    public async Task Cancelar_UnPedidoEnviado_Devuelve400ConElMotivo()
    {
        var (orderId, _, userToken) = await ShippedOrderAsync();

        var response = await CancelAsync(orderId, userToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("message").GetString()
            .Should().Contain("pide una devolución");
    }
}

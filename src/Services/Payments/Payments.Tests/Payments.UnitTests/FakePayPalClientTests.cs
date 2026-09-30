using Ecommerce.Payments.Infrastructure.PayPal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ecommerce.Payments.UnitTests;

public class FakePayPalClientTests
{
    private readonly FakePayPalClient _client = new(NullLogger<FakePayPalClient>.Instance);

    [Fact]
    public async Task CreateOrderAsync_DeberiaDevolverUnOrderIdSinApproveUrl()
    {
        var orderId = Guid.NewGuid();

        var result = await _client.CreateOrderAsync(50m, "USD", orderId, CancellationToken.None);

        result.PayPalOrderId.Should().Contain(orderId.ToString("N"));
        result.ApproveUrl.Should().BeNull("el storefront no debe intentar abrir ninguna pestaña en modo simulado");
    }

    [Fact]
    public async Task CaptureOrderAsync_SiempreDeberiaSerExitoso()
    {
        var result = await _client.CaptureOrderAsync("FAKE-123", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CaptureId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_DeberiaRechazarSiempre()
    {
        var result = await _client.VerifyWebhookSignatureAsync(
            new Dictionary<string, string>(), "{}", CancellationToken.None);

        result.Should().BeFalse("no debería llegar ningún webhook real mientras el modo simulado está activo");
    }
}

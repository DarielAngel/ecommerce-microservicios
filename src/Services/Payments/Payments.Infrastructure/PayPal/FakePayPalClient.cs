using Ecommerce.Payments.Application.Common;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payments.Infrastructure.PayPal;

/// <summary>
/// Implementación simulada de IPayPalClient, para desarrollo/pruebas locales sin necesitar
/// una cuenta real de PayPal. Se activa por configuración (PayPal:Provider=Fake) — el valor
/// por defecto sigue siendo el cliente real (PayPalClient); esto NUNCA se activa solo, hay
/// que pedirlo explícitamente. Nunca debe usarse en producción.
///
/// No es un cliente de prueba nuevo ni un atajo aparte: es la MISMA interfaz que ya usan los
/// tests de integración (ver FakePayPalClient en Payments.IntegrationTests) — la diferencia
/// es que este queda disponible también para correr dentro de Docker, no solo en xunit.
/// </summary>
public class FakePayPalClient : IPayPalClient
{
    private readonly ILogger<FakePayPalClient> _logger;

    public FakePayPalClient(ILogger<FakePayPalClient> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "PayPal:Provider=Fake está activo — los pagos NO son reales. Esto solo debe usarse en desarrollo local.");
    }

    public Task<CreatePayPalOrderResult> CreateOrderAsync(decimal amount, string currency, Guid internalOrderId, CancellationToken ct)
    {
        var fakeOrderId = $"FAKE-{internalOrderId:N}";
        _logger.LogInformation("[Fake PayPal] Orden creada: {FakeOrderId} por {Amount} {Currency}", fakeOrderId, amount, currency);

        // ApproveUrl null a propósito: el storefront ya maneja este caso (no intenta abrir
        // ninguna pestaña si no hay URL) — el usuario pasa directo a confirmar el pago.
        return Task.FromResult(new CreatePayPalOrderResult(fakeOrderId, ApproveUrl: null!));
    }

    public Task<CapturePayPalOrderResult> CaptureOrderAsync(string payPalOrderId, CancellationToken ct)
    {
        var fakeCaptureId = $"FAKE-CAPTURE-{payPalOrderId}";
        _logger.LogInformation("[Fake PayPal] Captura simulada exitosa: {FakeCaptureId}", fakeCaptureId);
        return Task.FromResult(new CapturePayPalOrderResult(true, fakeCaptureId, null));
    }

    public Task<bool> VerifyWebhookSignatureAsync(IDictionary<string, string> headers, string rawBody, CancellationToken ct)
    {
        // No debería llegar ningún webhook real mientras este modo está activo (no hay
        // PayPal de verdad del otro lado) — si algo llama a esto, lo rechazamos.
        _logger.LogWarning("[Fake PayPal] Se recibió un webhook mientras el modo simulado está activo — rechazado.");
        return Task.FromResult(false);
    }
}

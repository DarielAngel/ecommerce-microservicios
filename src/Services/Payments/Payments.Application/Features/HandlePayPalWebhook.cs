using System.Text.Json;
using Ecommerce.Payments.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payments.Application.Features;

public record HandlePayPalWebhookCommand(IDictionary<string, string> Headers, string RawBody) : IRequest<Unit>;

public class HandlePayPalWebhookCommandHandler : IRequestHandler<HandlePayPalWebhookCommand, Unit>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayPalClient _payPalClient;
    private readonly ILogger<HandlePayPalWebhookCommandHandler> _logger;

    public HandlePayPalWebhookCommandHandler(
        IPaymentRepository paymentRepository, IPayPalClient payPalClient, ILogger<HandlePayPalWebhookCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _payPalClient = payPalClient;
        _logger = logger;
    }

    public async Task<Unit> Handle(HandlePayPalWebhookCommand request, CancellationToken ct)
    {
        // Verificación de firma PRIMERO: nunca procesamos un webhook sin confirmar que
        // realmente viene de PayPal (si no, cualquiera podría llamar a este endpoint público
        // y marcar órdenes como pagadas sin haber pagado nada).
        var isValid = await _payPalClient.VerifyWebhookSignatureAsync(request.Headers, request.RawBody, ct);

        if (!isValid)
        {
            _logger.LogWarning("Webhook de PayPal con firma inválida — ignorado.");
            return Unit.Value;
        }

        using var document = JsonDocument.Parse(request.RawBody);
        var root = document.RootElement;

        var eventType = root.TryGetProperty("event_type", out var eventTypeProp) ? eventTypeProp.GetString() : null;
        var resource = root.TryGetProperty("resource", out var resourceProp) ? resourceProp : default;

        _logger.LogInformation("Webhook de PayPal recibido: {EventType}", eventType);

        switch (eventType)
        {
            case "PAYMENT.CAPTURE.COMPLETED":
                await HandleCaptureCompletedAsync(resource, ct);
                break;

            case "PAYMENT.CAPTURE.DENIED":
                await HandleCaptureDeniedAsync(resource, ct);
                break;

            default:
                // Cualquier otro tipo de evento (hay decenas) simplemente se reconoce y se ignora.
                _logger.LogInformation("Tipo de evento de PayPal no manejado explícitamente: {EventType}", eventType);
                break;
        }

        return Unit.Value;
    }

    private async Task HandleCaptureCompletedAsync(JsonElement resource, CancellationToken ct)
    {
        var payPalOrderId = ExtractRelatedOrderId(resource);
        var captureId = resource.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

        if (payPalOrderId is null || captureId is null)
        {
            _logger.LogWarning("Webhook PAYMENT.CAPTURE.COMPLETED sin order_id o capture id — ignorado.");
            return;
        }

        var payment = await _paymentRepository.GetByPayPalOrderIdAsync(payPalOrderId, ct);

        if (payment is null)
        {
            _logger.LogWarning("Webhook para una orden de PayPal que no tenemos registrada: {PayPalOrderId}", payPalOrderId);
            return;
        }

        payment.MarkCaptured(captureId); // idempotente: si ya estaba capturado con el mismo id, no hace nada
        await _paymentRepository.SaveChangesAsync(ct);
    }

    private async Task HandleCaptureDeniedAsync(JsonElement resource, CancellationToken ct)
    {
        var payPalOrderId = ExtractRelatedOrderId(resource);

        if (payPalOrderId is null)
        {
            return;
        }

        var payment = await _paymentRepository.GetByPayPalOrderIdAsync(payPalOrderId, ct);

        if (payment is null)
        {
            return;
        }

        payment.MarkFailed("PayPal denegó la captura del pago (webhook PAYMENT.CAPTURE.DENIED).");
        await _paymentRepository.SaveChangesAsync(ct);
    }

    private static string? ExtractRelatedOrderId(JsonElement resource)
    {
        // La ubicación exacta del order_id relacionado varía según el evento; esta es la
        // ruta estándar para eventos de captura: resource.supplementary_data.related_ids.order_id
        if (resource.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (resource.TryGetProperty("supplementary_data", out var supplementaryData) &&
            supplementaryData.TryGetProperty("related_ids", out var relatedIds) &&
            relatedIds.TryGetProperty("order_id", out var orderIdProp))
        {
            return orderIdProp.GetString();
        }

        return null;
    }
}

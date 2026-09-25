using Ecommerce.Payments.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payments.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ISender _mediator;

    public PaymentsController(ISender mediator)
    {
        _mediator = mediator;
    }

    public record CreatePaymentRequest(Guid OrderId, decimal Amount, string Currency);

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PaymentResult>> Create(CreatePaymentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePaymentCommand(request.OrderId, request.Amount, request.Currency), ct);
        return Ok(result);
    }

    [HttpPost("{orderId:guid}/capture")]
    [Authorize]
    public async Task<ActionResult<PaymentResult>> Capture(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new CapturePaymentCommand(orderId), ct);
        return Ok(result);
    }

    [HttpGet("{orderId:guid}")]
    [Authorize]
    public async Task<ActionResult<PaymentResult>> GetByOrderId(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentByOrderIdQuery(orderId), ct);
        return Ok(result);
    }

    /// <summary>
    /// A donde PayPal redirige al navegador del comprador después de que aprueba el pago.
    /// No hay frontend todavía, así que devolvemos una página simple con el siguiente paso
    /// (llamar a /capture) en vez de silenciosamente no hacer nada.
    /// </summary>
    [HttpGet("return")]
    [AllowAnonymous]
    public ContentResult Return([FromQuery] string? token, [FromQuery(Name = "PayerID")] string? payerId)
    {
        var html = $"""
            <html><body style="font-family: sans-serif; padding: 2rem;">
            <h2>Pago aprobado ✅</h2>
            <p>PayPal Order ID: <code>{token}</code></p>
            <p>Payer ID: <code>{payerId}</code></p>
            <p>Ahora hay que <b>capturar</b> el pago para completarlo. Busca el <code>orderId</code> interno
            que usaste al crear el pago y llama:</p>
            <pre>curl -X POST http://localhost:5000/api/payments/{"{orderId}"}/capture -H "Authorization: Bearer TU_TOKEN"</pre>
            <p>Puedes cerrar esta pestaña.</p>
            </body></html>
            """;
        return Content(html, "text/html");
    }

    [HttpGet("cancel")]
    [AllowAnonymous]
    public ContentResult Cancel()
    {
        var html = """
            <html><body style="font-family: sans-serif; padding: 2rem;">
            <h2>Pago cancelado</h2>
            <p>El comprador canceló antes de aprobar el pago. Puedes cerrar esta pestaña.</p>
            </body></html>
            """;
        return Content(html, "text/html");
    }

    /// <summary>
    /// Endpoint público (PayPal lo llama directamente, sin JWT): la seguridad acá es la
    /// verificación de firma dentro del handler, no un token de nuestro sistema.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        await _mediator.Send(new HandlePayPalWebhookCommand(headers, rawBody), ct);

        // PayPal solo necesita un 200 para no seguir reintentando la entrega del webhook.
        return Ok();
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ecommerce.Orders.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ISender _mediator;

    public OrdersController(ISender mediator)
    {
        _mediator = mediator;
    }

    public record CheckoutRequest(List<Guid> VariantIds, string ShippingAddress, string? CouponCode = null, bool UsePoints = false);

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Token sin un identificador de usuario válido.");
        }

        return userId;
    }

    /// <summary>
    /// Email y nombre ya vienen en el JWT que emite Users — los reusamos acá para guardarlos
    /// en la orden (y poder mandar notificaciones después) sin tener que llamar a Users por red.
    /// </summary>
    private (string Email, string FullName) GetUserEmailAndName()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? throw new UnauthorizedAccessException("Token sin email.");
        var fullName = User.FindFirstValue(ClaimTypes.Name) ?? "Cliente";

        return (email, fullName);
    }

    private string GetRawAccessToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..]
            : header;
    }

    /// <summary>
    /// Primer tramo de la saga: reserva stock y crea el pago. Devuelve el approveUrl que el
    /// comprador tiene que abrir en el navegador para aprobar el pago en PayPal.
    /// </summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResult>> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        var (email, fullName) = GetUserEmailAndName();
        var result = await _mediator.Send(
            new CheckoutCommand(GetUserId(), email, fullName, request.VariantIds, request.ShippingAddress, GetRawAccessToken(),
                request.CouponCode, request.UsePoints), ct);
        return Ok(result);
    }

    /// <summary>
    /// Segundo tramo: se llama DESPUÉS de que el comprador aprobó el pago en PayPal (ver la
    /// página de retorno de Pagos). Captura el pago y confirma/libera el stock según el resultado.
    /// </summary>
    [HttpPost("{orderId:guid}/confirm-payment")]
    public async Task<ActionResult<CheckoutResult>> ConfirmPayment(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmPaymentCommand(orderId, GetRawAccessToken(), GetUserId()), ct);
        return Ok(result);
    }

    /// <summary>Marca la orden como enviada (Admin) y dispara el email de "tu pedido va en camino".</summary>
    [HttpPost("{orderId:guid}/ship")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CheckoutResult>> MarkAsShipped(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new MarkOrderAsShippedCommand(orderId), ct);
        return Ok(result);
    }

    // ---- Devoluciones (Fase 7) ----

    public record RequestReturnRequest(List<ReturnItemInput> Items, string Reason, string? Comment);
    public record ResolveReturnRequest(string? Note);

    /// <summary>El cliente pide devolver productos de un pedido enviado (hasta 30 días después del envío).</summary>
    [HttpPost("{orderId:guid}/returns")]
    public async Task<ActionResult<CheckoutResult>> RequestReturn(Guid orderId, RequestReturnRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RequestReturnCommand(orderId, GetUserId(), request.Items ?? new(), request.Reason ?? "", request.Comment), ct);
        return Ok(result);
    }

    public record CancelOrderRequest(string? Reason, string? Comment);

    /// <summary>
    /// Cancela un pedido sin enviar: pendiente de pago → al instante; pagado → el cliente pide la cancelación (la
    /// aprueba un Admin) y un Admin la hace directamente (con reembolso total).
    /// </summary>
    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<CheckoutResult>> Cancel(Guid orderId, CancelOrderRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelOrderCommand(
            orderId, GetUserId(), User.IsInRole("Admin"), request?.Reason ?? "ChangedMind", request?.Comment, GetRawAccessToken()), ct);
        return Ok(result);
    }

    /// <summary>Todas las devoluciones (Admin), las más nuevas primero. ?status=Requested|Approved|Refunded|Rejected</summary>
    [HttpGet("returns")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<AdminReturnResult>>> ListReturns(
        [FromQuery] string? status, [FromQuery] int count = 100, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListReturnsQuery(status, count), ct);
        return Ok(result);
    }

    /// <summary>Aprueba y reembolsa (Admin). Repetirlo sobre una ya aprobada reintenta el reembolso.</summary>
    [HttpPost("returns/{returnId:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReturnResult>> ApproveReturn(Guid returnId, ResolveReturnRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApproveReturnCommand(returnId, request?.Note, GetRawAccessToken()), ct);
        return Ok(result);
    }

    [HttpPost("returns/{returnId:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReturnResult>> RejectReturn(Guid returnId, ResolveReturnRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RejectReturnCommand(returnId, request.Note ?? "", GetRawAccessToken()), ct);
        return Ok(result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<CheckoutResult>> GetById(Guid orderId, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        var result = await _mediator.Send(new GetOrderByIdQuery(orderId, GetUserId(), isAdmin), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CheckoutResult>>> ListMine(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListMyOrdersQuery(GetUserId()), ct);
        return Ok(result);
    }

    /// <summary>Todas las órdenes del sistema, con datos del comprador — solo Admin.</summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<AdminOrderResult>>> ListAll([FromQuery] int count = 100, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListAllOrdersQuery(count), ct);
        return Ok(result);
    }
}

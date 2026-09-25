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

    public record CheckoutRequest(List<Guid> VariantIds, string ShippingAddress);

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Token sin un identificador de usuario válido.");
        }

        return userId;
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
        var result = await _mediator.Send(
            new CheckoutCommand(GetUserId(), request.VariantIds, request.ShippingAddress, GetRawAccessToken()), ct);
        return Ok(result);
    }

    /// <summary>
    /// Segundo tramo: se llama DESPUÉS de que el comprador aprobó el pago en PayPal (ver la
    /// página de retorno de Pagos). Captura el pago y confirma/libera el stock según el resultado.
    /// </summary>
    [HttpPost("{orderId:guid}/confirm-payment")]
    public async Task<ActionResult<CheckoutResult>> ConfirmPayment(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmPaymentCommand(orderId, GetRawAccessToken()), ct);
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
}

using System.Security.Claims;
using Ecommerce.Cart.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Cart.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ISender _mediator;

    public CartController(ISender mediator)
    {
        _mediator = mediator;
    }

    public record AddItemRequest(Guid VariantId, int Quantity);
    public record UpdateQuantityRequest(int Quantity);

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Token sin un identificador de usuario válido.");
        }

        return userId;
    }

    /// <summary>El JWT crudo con el que llamó el usuario, para reenviarlo a Inventario.</summary>
    private string GetRawAccessToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..]
            : header;
    }

    [HttpGet]
    public async Task<ActionResult<CartResult>> GetCart(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCartQuery(GetUserId()), ct);
        return Ok(result);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResult>> AddItem(AddItemRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new AddItemCommand(GetUserId(), request.VariantId, request.Quantity, GetRawAccessToken()), ct);
        return Ok(result);
    }

    [HttpPut("items/{variantId:guid}")]
    public async Task<ActionResult<CartResult>> UpdateQuantity(Guid variantId, UpdateQuantityRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateItemQuantityCommand(GetUserId(), variantId, request.Quantity, GetRawAccessToken()), ct);
        return Ok(result);
    }

    [HttpDelete("items/{variantId:guid}")]
    public async Task<ActionResult<CartResult>> RemoveItem(Guid variantId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveItemCommand(GetUserId(), variantId), ct);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartResult>> Clear(CancellationToken ct)
    {
        var result = await _mediator.Send(new ClearCartCommand(GetUserId()), ct);
        return Ok(result);
    }
}

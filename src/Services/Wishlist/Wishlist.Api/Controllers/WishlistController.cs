using System.Security.Claims;
using Ecommerce.Wishlist.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Wishlist.Api.Controllers;

/// <summary>
/// Favoritos del cliente autenticado. Ningún endpoint recibe un id de usuario: siempre se toma del
/// token, así que es imposible ver o tocar la lista de otra persona.
/// </summary>
[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly ISender _mediator;

    public WishlistController(ISender mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
        }

        return userId;
    }

    /// <summary>Mis favoritos, del más reciente al más antiguo.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WishlistItemResult>>> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyWishlistQuery(GetUserId()), ct);
        return Ok(result);
    }

    /// <summary>Agrega un favorito. PUT porque es idempotente: repetirlo deja el mismo resultado.</summary>
    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> Add(Guid productId, CancellationToken ct)
    {
        await _mediator.Send(new AddToWishlistCommand(GetUserId(), productId), ct);
        return NoContent();
    }

    /// <summary>Quita un favorito. Si no estaba, también responde 204.</summary>
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveFromWishlistCommand(GetUserId(), productId), ct);
        return NoContent();
    }
}

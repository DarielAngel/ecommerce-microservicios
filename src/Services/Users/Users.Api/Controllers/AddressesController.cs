using System.Security.Claims;
using Ecommerce.Users.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Users.Api.Controllers;

/// <summary>
/// Libreta de direcciones del cliente que hace la llamada (Fase 5). No hay forma de pedir
/// direcciones de otro: el dueño siempre sale del token.
/// </summary>
[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly ISender _mediator;

    public AddressesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddressResult>>> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListMyAddressesQuery(CurrentUserId()), ct));

    [HttpPost]
    public async Task<ActionResult<AddressResult>> Add(AddressInput input, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddAddressCommand(CurrentUserId(), input), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{addressId:guid}")]
    public async Task<ActionResult<AddressResult>> Update(Guid addressId, AddressInput input, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateAddressCommand(CurrentUserId(), addressId, input), ct));

    /// <summary>Devuelve la libreta completa, ya reordenada (la predeterminada primero).</summary>
    [HttpPost("{addressId:guid}/default")]
    public async Task<ActionResult<IReadOnlyList<AddressResult>>> SetDefault(Guid addressId, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetDefaultAddressCommand(CurrentUserId(), addressId), ct));

    /// <summary>Devuelve la libreta que queda (si era la predeterminada, ya tiene otra).</summary>
    [HttpDelete("{addressId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AddressResult>>> Delete(Guid addressId, CancellationToken ct) =>
        Ok(await _mediator.Send(new DeleteAddressCommand(CurrentUserId(), addressId), ct));

    private Guid CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new Application.Common.UnauthorizedAppException("Sesión inválida.");
    }
}

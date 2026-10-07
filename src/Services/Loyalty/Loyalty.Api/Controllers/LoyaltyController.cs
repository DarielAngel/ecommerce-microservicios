using System.Security.Claims;
using Ecommerce.Loyalty.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Loyalty.Api.Controllers;

internal static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
        }

        return userId;
    }
}

/// <summary>Los puntos del cliente que hace la llamada (Fase 6).</summary>
[ApiController]
[Route("api/loyalty")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly ISender _mediator;

    public LoyaltyController(ISender mediator) => _mediator = mediator;

    /// <summary>Saldo, su valor en dinero, las reglas del programa y los últimos movimientos.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<LoyaltySummaryResult>> Me(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetMyPointsQuery(User.GetUserId()), ct));

    /// <summary>Cuántos puntos se usarían (y cuánto descuentan) en una compra de <paramref name="amount"/>.</summary>
    [HttpGet("me/quote")]
    public async Task<ActionResult<QuoteResult>> Quote([FromQuery] decimal amount, CancellationToken ct) =>
        Ok(await _mediator.Send(new QuoteRedemptionQuery(User.GetUserId(), amount), ct));
}

/// <summary>
/// Canje de puntos dentro de la saga de checkout de Órdenes. La ruta empieza con /internal: el Gateway
/// solo publica /api/loyalty, así que un cliente no puede apartar puntos llamando aquí directamente.
/// Igual exige el token del cliente (Órdenes lo reenvía): cada canje queda ligado a ese usuario.
/// </summary>
[ApiController]
[Route("internal/loyalty/redemptions")]
[Authorize]
public class PointsRedemptionsController : ControllerBase
{
    public record ReserveRequest(Guid OrderId, decimal Amount);

    private readonly ISender _mediator;

    public PointsRedemptionsController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<PointsRedemptionResult>> Reserve([FromBody] ReserveRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new ReservePointsCommand(request.OrderId, User.GetUserId(), request.Amount), ct));

    [HttpPost("{orderId:guid}/confirm")]
    public async Task<ActionResult<PointsRedemptionResult>> Confirm(Guid orderId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ConfirmPointsCommand(orderId, User.GetUserId()), ct));

    [HttpPost("{orderId:guid}/release")]
    public async Task<IActionResult> Release(Guid orderId, CancellationToken ct)
    {
        await _mediator.Send(new ReleasePointsCommand(orderId, User.GetUserId()), ct);
        return NoContent();
    }
}

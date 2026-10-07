using System.Security.Claims;
using Ecommerce.Promotions.Application.Features;
using Ecommerce.Promotions.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Promotions.Api.Controllers;

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

/// <summary>Cupones: el cliente los valida en el checkout; el Admin los crea y administra.</summary>
[ApiController]
[Route("api/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    public record SaveCouponRequest(
        string? Code, string Description, DiscountType Type, decimal Value, decimal? MaxDiscountAmount,
        decimal MinimumSubtotal, DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit,
        bool OncePerCustomer, bool IsActive = true)
    {
        public CouponInput ToInput() => new(Description, Type, Value, MaxDiscountAmount, MinimumSubtotal,
            StartsAtUtc, EndsAtUtc, UsageLimit, OncePerCustomer, IsActive);
    }

    private readonly ISender _mediator;

    public CouponsController(ISender mediator) => _mediator = mediator;

    /// <summary>"¿Cuánto me descuenta?" para el subtotal actual. No aparta el cupón.</summary>
    [HttpGet("validate")]
    public async Task<ActionResult<CouponQuoteResult>> Validate(
        [FromQuery] string? code, [FromQuery] decimal subtotal, CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidateCouponQuery(code ?? string.Empty, User.GetUserId(), subtotal), ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<CouponAdminResult>>> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListCouponsQuery(), ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CouponAdminResult>> Create([FromBody] SaveCouponRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCouponCommand(request.Code ?? string.Empty, request.ToInput()), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CouponAdminResult>> Update(Guid id, [FromBody] SaveCouponRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateCouponCommand(id, request.ToInput()), ct));
}

/// <summary>
/// Canjes de cupones, usados por Órdenes durante la saga de checkout. La ruta empieza con /internal
/// a propósito: el Gateway solo publica /api/coupons, así que un cliente no puede apartar usos de un
/// cupón limitado llamando aquí directamente. Aun así exige el token del cliente (Órdenes lo reenvía)
/// y cada canje queda ligado a ese usuario.
/// </summary>
[ApiController]
[Route("internal/redemptions")]
[Authorize]
public class RedemptionsController : ControllerBase
{
    public record ReserveRequest(Guid OrderId, string Code, decimal Subtotal);

    private readonly ISender _mediator;

    public RedemptionsController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<RedemptionResult>> Reserve([FromBody] ReserveRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new ReserveRedemptionCommand(request.OrderId, request.Code, User.GetUserId(), request.Subtotal), ct));

    [HttpPost("{orderId:guid}/confirm")]
    public async Task<ActionResult<RedemptionResult>> Confirm(Guid orderId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ConfirmRedemptionCommand(orderId, User.GetUserId()), ct));

    [HttpPost("{orderId:guid}/release")]
    public async Task<IActionResult> Release(Guid orderId, CancellationToken ct)
    {
        await _mediator.Send(new ReleaseRedemptionCommand(orderId, User.GetUserId()), ct);
        return NoContent();
    }
}

using Ecommerce.Inventory.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Inventory.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly ISender _mediator;

    public StockController(ISender mediator)
    {
        _mediator = mediator;
    }

    public record AdjustStockRequest(int NewQuantityOnHand);
    public record ReservationLineRequest(Guid VariantId, int Quantity);
    public record ReserveStockRequest(Guid OrderId, List<ReservationLineRequest> Items);

    /// <summary>
    /// Disponibilidad PÚBLICA para la tienda (insignias "Quedan pocas unidades" / "Agotado"), de varias
    /// variantes a la vez: ?variantIds=a&amp;variantIds=b. No requiere sesión y no expone el stock exacto
    /// cuando hay de sobra. Se declara antes de "{variantId:guid}" solo por claridad (no chocan).
    /// </summary>
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<VariantAvailability>>> Availability(
        [FromQuery] Guid[] variantIds, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAvailabilityQuery(variantIds), ct));

    /// <summary>Consulta de stock de una variante. Cualquier usuario autenticado puede verla.</summary>
    [HttpGet("{variantId:guid}")]
    public async Task<ActionResult<StockResult>> GetStock(Guid variantId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStockQuery(variantId), ct);
        return Ok(result);
    }

    /// <summary>Variantes con stock disponible en o bajo su umbral configurado (RF4.4).</summary>
    [HttpGet("low-stock")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<StockResult>>> ListLowStock(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListLowStockQuery(), ct);
        return Ok(result);
    }

    /// <summary>Ajuste manual de stock físico (recepción de mercadería, conteo, corrección).</summary>
    [HttpPost("{variantId:guid}/adjust")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StockResult>> AdjustStock(Guid variantId, AdjustStockRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AdjustStockCommand(variantId, request.NewQuantityOnHand), ct);
        return Ok(result);
    }

    /// <summary>
    /// Reserva stock para una orden (todo o nada, varias líneas). Lo llama el microservicio
    /// de Órdenes en nombre del usuario que está haciendo checkout — por eso solo exige estar
    /// autenticado (no rol Admin): cualquier Cliente puede reservar stock para SU PROPIA orden.
    /// </summary>
    [HttpPost("reservations")]
    public async Task<ActionResult<ReservationResult>> Reserve(ReserveStockRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new ReservationLineInput(i.VariantId, i.Quantity)).ToList();
        var result = await _mediator.Send(new ReserveStockCommand(request.OrderId, items), ct);
        return Ok(result);
    }

    /// <summary>Confirma una reserva ya hecha: descuenta stock físico de forma definitiva.</summary>
    [HttpPost("reservations/{orderId:guid}/confirm")]
    public async Task<ActionResult<ReservationResult>> Confirm(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmReservationCommand(orderId), ct);
        return Ok(result);
    }

    /// <summary>Libera una reserva (pago fallido/cancelado): el stock vuelve a estar disponible.</summary>
    [HttpPost("reservations/{orderId:guid}/release")]
    public async Task<ActionResult<ReservationResult>> Release(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReleaseReservationCommand(orderId), ct);
        return Ok(result);
    }
}

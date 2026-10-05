using System.Security.Claims;
using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Reviews.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    public record SaveReviewRequest(int Rating, string Title, string? Comment);

    private readonly ISender _mediator;

    public ReviewsController(ISender mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
        }

        return userId;
    }

    private string GetFullName() =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    // ---- Lectura pública ----

    [HttpGet("products/{productId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ReviewResult>>> List(
        Guid productId,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListProductReviewsQuery(productId, sort, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("products/{productId:guid}/summary")]
    [AllowAnonymous]
    public async Task<ActionResult<RatingSummaryResult>> Summary(Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRatingSummaryQuery(productId), ct);
        return Ok(result);
    }

    /// <summary>Resumen de varios productos en una sola llamada: ?productIds=a&amp;productIds=b</summary>
    [HttpGet("summaries")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RatingSummaryResult>>> Summaries(
        [FromQuery] Guid[] productIds, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRatingSummariesQuery(productIds), ct);
        return Ok(result);
    }

    // ---- Acciones del cliente autenticado ----

    [HttpGet("products/{productId:guid}/mine")]
    [Authorize]
    public async Task<ActionResult<ReviewResult>> Mine(Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyReviewQuery(productId, GetUserId()), ct);

        return result is null
            ? NotFound(new { message = "Todavía no reseñaste este producto." })
            : Ok(result);
    }

    [HttpPost("products/{productId:guid}")]
    [Authorize]
    public async Task<ActionResult<ReviewResult>> Create(Guid productId, [FromBody] SaveReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateReviewCommand(productId, GetUserId(), GetFullName(), request.Rating, request.Title, request.Comment), ct);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{reviewId:guid}")]
    [Authorize]
    public async Task<ActionResult<ReviewResult>> Update(Guid reviewId, [FromBody] SaveReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateReviewCommand(reviewId, GetUserId(), request.Rating, request.Title, request.Comment), ct);

        return Ok(result);
    }

    /// <summary>El autor borra la suya; un Admin puede borrar cualquiera (moderación).</summary>
    [HttpDelete("{reviewId:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid reviewId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteReviewCommand(reviewId, GetUserId(), User.IsInRole("Admin")), ct);
        return NoContent();
    }
}

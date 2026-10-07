using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IFileStorage _fileStorage;

    public ProductsController(ISender mediator, IFileStorage fileStorage)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
    }

    public record CreateProductRequest(string Name, string Description, Guid CategoryId, List<VariantInput> Variants);
    public record UpdateProductRequest(string Name, string Description, Guid CategoryId, bool IsActive);

    /// <summary>
    /// Búsqueda pública de catálogo: cualquiera puede navegar y buscar sin estar logueado.
    /// Coincide con nombre/descripción de forma parcial, y admite filtros de categoría y precio.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductSummary>>> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? sortBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        // includeInactive solo vale para un Admin: un visitante que lo mande igual ve solo los activos.
        var result = await _mediator.Send(
            new SearchProductsQuery(searchTerm, categoryId, minPrice, maxPrice, sortBy, page, pageSize,
                includeInactive && User.IsInRole("Admin")), ct);
        return Ok(result);
    }

    /// <summary>Autocompletado del buscador (T4.1). Con menos de 2 letras devuelve una lista vacía.</summary>
    [HttpGet("suggestions")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductSummary>>> Suggestions(
        [FromQuery] string? q, [FromQuery] int limit = 6, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new SuggestProductsQuery(q, limit), ct));

    /// <summary>"También te puede interesar" (T4.2): misma categoría primero, luego categorías hermanas.</summary>
    [HttpGet("{id:guid}/related")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductSummary>>> Related(
        Guid id, [FromQuery] int limit = 8, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetRelatedProductsQuery(id, limit), ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDetailResult>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Consulta de una variante individual (no del producto completo). Usado por otros
    /// microservicios (ej. Carrito) que solo conocen el VariantId. Público, igual que el resto
    /// de las consultas de catálogo.
    /// </summary>
    [HttpGet("variants/{variantId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<VariantDetailsResult>> GetVariantById(Guid variantId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVariantByIdQuery(variantId), ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDetailResult>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateProductCommand(request.Name, request.Description, request.CategoryId, request.Variants), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDetailResult>> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateProductCommand(id, request.Name, request.Description, request.CategoryId, request.IsActive), ct);
        return Ok(result);
    }

    /// <summary>Sube una imagen real (multipart/form-data) y la asocia al producto.</summary>
    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ImageResult>> UploadImage(
        Guid id, IFormFile file, [FromForm] bool isPrimary, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Debes adjuntar un archivo de imagen." });
        }

        await using var stream = file.OpenReadStream();
        var savedFileName = await _fileStorage.SaveAsync(stream, file.FileName, file.ContentType, ct);

        var result = await _mediator.Send(new AddProductImageCommand(id, savedFileName, isPrimary), ct);
        return Ok(result);
    }
}

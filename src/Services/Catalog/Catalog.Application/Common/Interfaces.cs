using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record ProductSearchFilter(
    string? SearchTerm,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SortBy,
    int Page,
    int PageSize,
    bool IncludeInactive = false);

public record ProductSummary(
    Guid Id,
    string Name,
    string Slug,
    Guid CategoryId,
    string CategoryName,
    decimal? MinPrice,
    string? PrimaryImageFileName,
    bool IsActive);

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca el producto dueño de una variante específica (para consultas por VariantId, sin conocer el ProductId).</summary>
    Task<Product?> GetByVariantIdAsync(Guid variantId, CancellationToken ct);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct);
    Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchFilter filter, CancellationToken ct);

    /// <summary>
    /// Productos ACTIVOS cuyo nombre contiene el texto (sin distinguir mayúsculas ni tildes),
    /// primero los que empiezan con él. Para el autocompletado del buscador.
    /// </summary>
    Task<IReadOnlyList<ProductSummary>> SuggestAsync(string term, int limit, CancellationToken ct);

    /// <summary>
    /// Productos ACTIVOS de la misma categoría (sin el propio producto). Si no alcanzan, completa
    /// con productos de categorías hermanas (mismo padre). Vacío si el producto no existe.
    /// </summary>
    Task<IReadOnlyList<ProductSummary>> GetRelatedAsync(Guid productId, int limit, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);

    /// <summary>
    /// Marca explícitamente una imagen recién agregada a un producto YA CARGADO como "Added".
    /// Necesario porque, a diferencia de crear un producto nuevo (donde Add() cascada
    /// correctamente a los hijos), agregar un hijo a un agregado ya rastreado por EF Core
    /// depende de la detección automática de cambios, que no siempre clasifica correctamente
    /// un ítem nuevo añadido a una colección "owned" de un dueño ya existente.
    /// </summary>
    void TrackNewImage(ProductImage image);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Abstrae dónde se guardan físicamente los archivos (disco local hoy, S3/Blob mañana).</summary>
public interface IFileStorage
{
    /// <returns>El nombre/ruta bajo el cual quedó guardado el archivo.</returns>
    Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct);
}

/// <summary>
/// Abstrae la publicación de eventos de integración hacia otros microservicios.
/// Application no sabe (ni le importa) que por debajo esto es RabbitMQ vía MassTransit;
/// eso vive enteramente en Infrastructure.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class;
}

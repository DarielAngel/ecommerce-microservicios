using Ecommerce.Catalog.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record SearchProductsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SortBy = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProductSummary>>;

public class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    // "name" (por defecto) es el único valor sin cambios de comportamiento respecto a antes.
    private static readonly string[] ValidSortValues =
        ["name", "price_asc", "price_desc", "newest"];

    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .WithMessage("El precio máximo debe ser mayor o igual al precio mínimo.");
        RuleFor(x => x.SortBy)
            .Must(s => ValidSortValues.Contains(s))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage($"sortBy debe ser uno de: {string.Join(", ", ValidSortValues)}.");
    }
}

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, PagedResult<ProductSummary>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<PagedResult<ProductSummary>> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        var filter = new ProductSearchFilter(
            request.SearchTerm,
            request.CategoryId,
            request.MinPrice,
            request.MaxPrice,
            request.SortBy,
            request.Page,
            request.PageSize);

        return _productRepository.SearchAsync(filter, ct);
    }
}

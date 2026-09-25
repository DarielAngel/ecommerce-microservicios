using Ecommerce.Catalog.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record SearchProductsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProductSummary>>;

public class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .WithMessage("El precio máximo debe ser mayor o igual al precio mínimo.");
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
            request.Page,
            request.PageSize);

        return _productRepository.SearchAsync(filter, ct);
    }
}

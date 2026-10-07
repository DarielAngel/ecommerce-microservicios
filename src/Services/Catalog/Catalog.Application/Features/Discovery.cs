using Ecommerce.Catalog.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

// ---------------------------------------------------------------------------------------------
// T4.1 Sugerencias mientras se escribe
// ---------------------------------------------------------------------------------------------

/// <summary>Autocompletado del buscador: pocos resultados, rápidos, solo productos activos.</summary>
public record SuggestProductsQuery(string? Term, int Limit = 6) : IRequest<IReadOnlyList<ProductSummary>>;

public class SuggestProductsQueryValidator : AbstractValidator<SuggestProductsQuery>
{
    public const int MaxLimit = 10;

    public SuggestProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit)
            .WithMessage($"limit debe estar entre 1 y {MaxLimit}.");
        RuleFor(x => x.Term).MaximumLength(100)
            .WithMessage("La búsqueda no puede superar 100 caracteres.");
    }
}

public class SuggestProductsQueryHandler : IRequestHandler<SuggestProductsQuery, IReadOnlyList<ProductSummary>>
{
    /// <summary>Con menos letras las sugerencias son ruido (y una consulta por cada tecla).</summary>
    public const int MinTermLength = 2;

    private readonly IProductRepository _products;

    public SuggestProductsQueryHandler(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<ProductSummary>> Handle(SuggestProductsQuery request, CancellationToken ct)
    {
        var term = (request.Term ?? string.Empty).Trim();
        if (term.Length < MinTermLength) return Array.Empty<ProductSummary>();

        return await _products.SuggestAsync(term, request.Limit, ct);
    }
}

// ---------------------------------------------------------------------------------------------
// T4.2 "También te puede interesar"
// ---------------------------------------------------------------------------------------------

public record GetRelatedProductsQuery(Guid ProductId, int Limit = 8) : IRequest<IReadOnlyList<ProductSummary>>;

public class GetRelatedProductsQueryValidator : AbstractValidator<GetRelatedProductsQuery>
{
    public const int MaxLimit = 20;

    public GetRelatedProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).WithMessage($"limit debe estar entre 1 y {MaxLimit}.");
    }
}

public class GetRelatedProductsQueryHandler : IRequestHandler<GetRelatedProductsQuery, IReadOnlyList<ProductSummary>>
{
    private readonly IProductRepository _products;

    public GetRelatedProductsQueryHandler(IProductRepository products) => _products = products;

    /// <summary>Un producto inexistente devuelve lista vacía: es un extra de la página, no debe romperla.</summary>
    public Task<IReadOnlyList<ProductSummary>> Handle(GetRelatedProductsQuery request, CancellationToken ct) =>
        _products.GetRelatedAsync(request.ProductId, request.Limit, ct);
}

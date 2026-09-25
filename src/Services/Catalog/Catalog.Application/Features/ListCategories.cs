using Ecommerce.Catalog.Application.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record ListCategoriesQuery : IRequest<IReadOnlyList<CategoryResult>>;

public class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public ListCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<CategoryResult>> Handle(ListCategoriesQuery request, CancellationToken ct)
    {
        var categories = await _categoryRepository.ListAllAsync(ct);

        return categories
            .Select(c => new CategoryResult(c.Id, c.Name, c.Slug, c.ParentCategoryId))
            .ToList();
    }
}

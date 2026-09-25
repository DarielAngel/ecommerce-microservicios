using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record CreateCategoryCommand(string Name, Guid? ParentCategoryId) : IRequest<CategoryResult>;

public record CategoryResult(Guid Id, string Name, string Slug, Guid? ParentCategoryId);

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryResult>
{
    private readonly ICategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryResult> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (await _categoryRepository.ExistsByNameAsync(request.Name, ct))
        {
            throw new ConflictAppException($"Ya existe una categoría llamada '{request.Name}'.");
        }

        if (request.ParentCategoryId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(request.ParentCategoryId.Value, ct)
                ?? throw new NotFoundAppException("La categoría padre indicada no existe.");
        }

        var category = Category.Create(request.Name, request.ParentCategoryId);
        await _categoryRepository.AddAsync(category, ct);
        await _categoryRepository.SaveChangesAsync(ct);

        return new CategoryResult(category.Id, category.Name, category.Slug, category.ParentCategoryId);
    }
}

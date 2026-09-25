using Ecommerce.Catalog.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Description,
    Guid CategoryId,
    bool IsActive) : IRequest<ProductDetailResult>;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDetailResult>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public UpdateProductCommandHandler(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<ProductDetailResult> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundAppException("El producto no existe.");

        _ = await _categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundAppException("La categoría indicada no existe.");

        product.UpdateDetails(request.Name, request.Description, request.CategoryId);

        if (request.IsActive) product.Activate();
        else product.Deactivate();

        await _productRepository.SaveChangesAsync(ct);

        return CreateProductCommandHandler.MapToDetail(product);
    }
}

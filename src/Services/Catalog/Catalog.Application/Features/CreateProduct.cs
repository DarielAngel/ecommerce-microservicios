using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Contracts.Events;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record VariantInput(string Sku, decimal Price, Dictionary<string, string> Attributes);

public record CreateProductCommand(
    string Name,
    string Description,
    Guid CategoryId,
    List<VariantInput> Variants) : IRequest<ProductDetailResult>;

public record VariantResult(Guid Id, string Sku, decimal Price, IReadOnlyDictionary<string, string> Attributes, bool IsActive);

public record ImageResult(Guid Id, string FileName, bool IsPrimary, int DisplayOrder);

public record ProductDetailResult(
    Guid Id,
    string Name,
    string Description,
    string Slug,
    Guid CategoryId,
    bool IsActive,
    IReadOnlyList<VariantResult> Variants,
    IReadOnlyList<ImageResult> Images);

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Variants).NotEmpty().WithMessage("El producto debe tener al menos una variante.");
        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Sku).NotEmpty();
            variant.RuleFor(v => v.Price).GreaterThan(0);
        });
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDetailResult>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisher _eventPublisher;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IEventPublisher eventPublisher)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<ProductDetailResult> Handle(CreateProductCommand request, CancellationToken ct)
    {
        _ = await _categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundAppException("La categoría indicada no existe.");

        foreach (var variant in request.Variants)
        {
            if (await _productRepository.ExistsBySkuAsync(variant.Sku, ct))
            {
                throw new ConflictAppException($"Ya existe una variante con el SKU '{variant.Sku}'.");
            }
        }

        var product = Product.Create(request.Name, request.Description, request.CategoryId);

        foreach (var variant in request.Variants)
        {
            product.AddVariant(variant.Sku, variant.Price, variant.Attributes);
        }

        await _productRepository.AddAsync(product, ct);
        await _productRepository.SaveChangesAsync(ct);

        // Se publica DESPUÉS de que el guardado tuvo éxito (no antes): si publicáramos primero
        // y el SaveChanges fallara después, Inventario crearía registros de stock para
        // variantes que en realidad nunca llegaron a existir en Catálogo.
        foreach (var variant in product.Variants)
        {
            await _eventPublisher.PublishAsync(
                new VariantCreatedEvent(variant.Id, product.Id, variant.Sku, DateTime.UtcNow), ct);
        }

        return MapToDetail(product);
    }

    internal static ProductDetailResult MapToDetail(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Slug,
        product.CategoryId,
        product.IsActive,
        product.Variants.Select(v => new VariantResult(v.Id, v.Sku, v.Price, v.Attributes, v.IsActive)).ToList(),
        product.Images.Select(i => new ImageResult(i.Id, i.FileName, i.IsPrimary, i.DisplayOrder)).ToList());
}

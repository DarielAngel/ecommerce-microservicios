using Ecommerce.Catalog.Application.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record GetVariantByIdQuery(Guid VariantId) : IRequest<VariantDetailsResult>;

public record VariantDetailsResult(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Price,
    bool IsActive);

public class GetVariantByIdQueryHandler : IRequestHandler<GetVariantByIdQuery, VariantDetailsResult>
{
    private readonly IProductRepository _productRepository;

    public GetVariantByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<VariantDetailsResult> Handle(GetVariantByIdQuery request, CancellationToken ct)
    {
        var product = await _productRepository.GetByVariantIdAsync(request.VariantId, ct)
            ?? throw new NotFoundAppException("La variante no existe.");

        var variant = product.Variants.First(v => v.Id == request.VariantId);

        return new VariantDetailsResult(
            variant.Id, product.Id, product.Name, variant.Sku, variant.Price, variant.IsActive && product.IsActive);
    }
}

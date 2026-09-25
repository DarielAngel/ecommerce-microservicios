using Ecommerce.Catalog.Application.Common;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDetailResult>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailResult>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDetailResult> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundAppException("El producto no existe.");

        return CreateProductCommandHandler.MapToDetail(product);
    }
}

using Ecommerce.Catalog.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Features;

public record AddProductImageCommand(Guid ProductId, string SavedFileName, bool IsPrimary) : IRequest<ImageResult>;

public class AddProductImageCommandValidator : AbstractValidator<AddProductImageCommand>
{
    public AddProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SavedFileName).NotEmpty();
    }
}

public class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, ImageResult>
{
    private readonly IProductRepository _productRepository;

    public AddProductImageCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ImageResult> Handle(AddProductImageCommand request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundAppException("El producto no existe.");

        var image = product.AddImage(request.SavedFileName, request.IsPrimary);
        _productRepository.TrackNewImage(image);
        await _productRepository.SaveChangesAsync(ct);

        return new ImageResult(image.Id, image.FileName, image.IsPrimary, image.DisplayOrder);
    }
}

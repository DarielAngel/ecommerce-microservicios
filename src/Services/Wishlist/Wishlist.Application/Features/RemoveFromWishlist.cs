using Ecommerce.Wishlist.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Wishlist.Application.Features;

/// <summary>Quita un producto de favoritos. Idempotente: quitar algo que no está no es un error.</summary>
public record RemoveFromWishlistCommand(Guid UserId, Guid ProductId) : IRequest;

public class RemoveFromWishlistCommandValidator : AbstractValidator<RemoveFromWishlistCommand>
{
    public RemoveFromWishlistCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
    }
}

public class RemoveFromWishlistCommandHandler : IRequestHandler<RemoveFromWishlistCommand>
{
    private readonly IWishlistRepository _wishlist;

    public RemoveFromWishlistCommandHandler(IWishlistRepository wishlist) => _wishlist = wishlist;

    public Task Handle(RemoveFromWishlistCommand request, CancellationToken ct) =>
        _wishlist.RemoveAsync(request.UserId, request.ProductId, ct);
}

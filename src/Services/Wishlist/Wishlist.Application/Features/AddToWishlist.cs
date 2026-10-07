using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Wishlist.Application.Features;

/// <summary>Agrega un producto a favoritos. Idempotente: agregarlo dos veces deja un solo favorito.</summary>
public record AddToWishlistCommand(Guid UserId, Guid ProductId) : IRequest;

public class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
    }
}

public class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand>
{
    private readonly IWishlistRepository _wishlist;

    public AddToWishlistCommandHandler(IWishlistRepository wishlist) => _wishlist = wishlist;

    public async Task Handle(AddToWishlistCommand request, CancellationToken ct)
    {
        // Si ya está, no hay nada que hacer — y no debe chocar con el tope aunque la lista esté llena.
        if (await _wishlist.ExistsAsync(request.UserId, request.ProductId, ct))
            return;

        if (await _wishlist.CountAsync(request.UserId, ct) >= WishlistItem.MaxItemsPerUser)
            throw new ConflictAppException(
                $"Tu lista de favoritos está llena (máximo {WishlistItem.MaxItemsPerUser}). Quita alguno para agregar otro.");

        await _wishlist.AddIfMissingAsync(WishlistItem.Create(request.UserId, request.ProductId), ct);
    }
}

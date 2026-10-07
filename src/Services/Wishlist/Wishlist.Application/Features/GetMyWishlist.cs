using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Domain.Entities;
using MediatR;

namespace Ecommerce.Wishlist.Application.Features;

/// <summary>Un favorito tal como lo ve su dueño. No incluye el id del usuario: ya es el del token.</summary>
public record WishlistItemResult(Guid ProductId, DateTime AddedAtUtc)
{
    public static WishlistItemResult From(WishlistItem item) => new(item.ProductId, item.AddedAtUtc);
}

/// <summary>Los favoritos del usuario autenticado, del más reciente al más antiguo.</summary>
public record GetMyWishlistQuery(Guid UserId) : IRequest<IReadOnlyList<WishlistItemResult>>;

public class GetMyWishlistQueryHandler : IRequestHandler<GetMyWishlistQuery, IReadOnlyList<WishlistItemResult>>
{
    private readonly IWishlistRepository _wishlist;

    public GetMyWishlistQueryHandler(IWishlistRepository wishlist) => _wishlist = wishlist;

    public async Task<IReadOnlyList<WishlistItemResult>> Handle(GetMyWishlistQuery request, CancellationToken ct)
    {
        var items = await _wishlist.ListAsync(request.UserId, ct);
        return items.Select(WishlistItemResult.From).ToList();
    }
}

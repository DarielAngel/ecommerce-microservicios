using Ecommerce.Cart.Application.Common;
using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using MediatR;

namespace Ecommerce.Cart.Application.Features;

public record GetCartQuery(Guid UserId) : IRequest<CartResult>;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartResult>
{
    private readonly ICartRepository _cartRepository;

    public GetCartQueryHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<CartResult> Handle(GetCartQuery request, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct)
            ?? CartAggregate.CreateForUser(request.UserId); // no persistido: un carrito vacío no necesita fila en BD

        return AddItemCommandHandler.MapToResult(cart);
    }
}

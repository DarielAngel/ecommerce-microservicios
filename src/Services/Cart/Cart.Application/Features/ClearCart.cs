using Ecommerce.Cart.Application.Common;
using MediatR;

namespace Ecommerce.Cart.Application.Features;

public record ClearCartCommand(Guid UserId) : IRequest<CartResult>;

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, CartResult>
{
    private readonly ICartRepository _cartRepository;

    public ClearCartCommandHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<CartResult> Handle(ClearCartCommand request, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundAppException("No tienes un carrito todavía.");

        cart.Clear();
        await _cartRepository.SaveChangesAsync(ct);

        return AddItemCommandHandler.MapToResult(cart);
    }
}

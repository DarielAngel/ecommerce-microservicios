using Ecommerce.Cart.Application.Common;
using MediatR;

namespace Ecommerce.Cart.Application.Features;

public record RemoveItemCommand(Guid UserId, Guid VariantId) : IRequest<CartResult>;

public class RemoveItemCommandHandler : IRequestHandler<RemoveItemCommand, CartResult>
{
    private readonly ICartRepository _cartRepository;

    public RemoveItemCommandHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<CartResult> Handle(RemoveItemCommand request, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundAppException("No tienes un carrito todavía.");

        cart.RemoveItem(request.VariantId);
        await _cartRepository.SaveChangesAsync(ct);

        return AddItemCommandHandler.MapToResult(cart);
    }
}

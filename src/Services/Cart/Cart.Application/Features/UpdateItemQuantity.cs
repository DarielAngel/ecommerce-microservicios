using Ecommerce.Cart.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Cart.Application.Features;

public record UpdateItemQuantityCommand(Guid UserId, Guid VariantId, int Quantity, string AccessToken) : IRequest<CartResult>;

public class UpdateItemQuantityCommandValidator : AbstractValidator<UpdateItemQuantityCommand>
{
    public UpdateItemQuantityCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0)
            .WithMessage("La cantidad no puede ser negativa (usa 0 para quitar el ítem).");
    }
}

public class UpdateItemQuantityCommandHandler : IRequestHandler<UpdateItemQuantityCommand, CartResult>
{
    private readonly ICartRepository _cartRepository;
    private readonly IInventoryServiceClient _inventoryClient;

    public UpdateItemQuantityCommandHandler(ICartRepository cartRepository, IInventoryServiceClient inventoryClient)
    {
        _cartRepository = cartRepository;
        _inventoryClient = inventoryClient;
    }

    public async Task<CartResult> Handle(UpdateItemQuantityCommand request, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundAppException("No tienes un carrito todavía.");

        // Si Quantity es 0, SetItemQuantity ya lo interpreta como "quitar" — no hace falta
        // validar stock para una cantidad que va a terminar en cero.
        if (request.Quantity > 0)
        {
            var availableStock = await _inventoryClient.GetAvailableQuantityAsync(request.VariantId, request.AccessToken, ct);

            if (request.Quantity > availableStock)
            {
                throw new InsufficientStockAppException(
                    $"No hay suficiente stock. Disponible: {availableStock}, pediste {request.Quantity}.");
            }
        }

        cart.SetItemQuantity(request.VariantId, request.Quantity);
        await _cartRepository.SaveChangesAsync(ct);

        return AddItemCommandHandler.MapToResult(cart);
    }
}

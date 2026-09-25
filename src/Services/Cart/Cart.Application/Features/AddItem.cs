using Ecommerce.Cart.Application.Common;
using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using FluentValidation;
using MediatR;

namespace Ecommerce.Cart.Application.Features;

public record AddItemCommand(Guid UserId, Guid VariantId, int Quantity, string AccessToken) : IRequest<CartResult>;

public record CartItemResult(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);
public record CartResult(Guid UserId, IReadOnlyList<CartItemResult> Items, decimal Subtotal, int TotalItemCount);

public class AddItemCommandValidator : AbstractValidator<AddItemCommand>
{
    public AddItemCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
    }
}

public class AddItemCommandHandler : IRequestHandler<AddItemCommand, CartResult>
{
    private readonly ICartRepository _cartRepository;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly IInventoryServiceClient _inventoryClient;

    public AddItemCommandHandler(
        ICartRepository cartRepository,
        ICatalogServiceClient catalogClient,
        IInventoryServiceClient inventoryClient)
    {
        _cartRepository = cartRepository;
        _catalogClient = catalogClient;
        _inventoryClient = inventoryClient;
    }

    public async Task<CartResult> Handle(AddItemCommand request, CancellationToken ct)
    {
        var variant = await _catalogClient.GetVariantAsync(request.VariantId, ct)
            ?? throw new NotFoundAppException("El producto/variante no existe.");

        if (!variant.IsActive)
        {
            throw new NotFoundAppException("Este producto ya no está disponible.");
        }

        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct);
        var isNewCart = cart is null;
        cart ??= CartAggregate.CreateForUser(request.UserId);

        // La cantidad total que quedaría en el carrito (lo que ya había + lo nuevo) es lo que
        // debe caber en el stock disponible, no solo la cantidad de esta llamada.
        var currentQuantityInCart = cart.GetCurrentQuantity(request.VariantId);
        var totalRequested = currentQuantityInCart + request.Quantity;

        var availableStock = await _inventoryClient.GetAvailableQuantityAsync(request.VariantId, request.AccessToken, ct);

        if (totalRequested > availableStock)
        {
            throw new InsufficientStockAppException(
                $"No hay suficiente stock. Disponible: {availableStock}, en tu carrito ya tienes {currentQuantityInCart}, pediste agregar {request.Quantity} más.");
        }

        var newItem = cart.AddItem(variant.VariantId, variant.ProductId, variant.ProductName, variant.Sku, variant.Price, request.Quantity);

        if (isNewCart)
        {
            await _cartRepository.AddAsync(cart, ct);
        }
        else if (newItem is not null)
        {
            _cartRepository.TrackNewItem(newItem);
        }

        await _cartRepository.SaveChangesAsync(ct);

        return MapToResult(cart);
    }

    internal static CartResult MapToResult(CartAggregate cart) => new(
        cart.UserId,
        cart.Items.Select(i => new CartItemResult(
            i.VariantId, i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
        cart.Subtotal,
        cart.TotalItemCount);
}

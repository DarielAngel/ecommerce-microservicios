using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Features;
using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Cart.UnitTests;

public class AddItemCommandHandlerTests
{
    private readonly ICartRepository _cartRepository = Substitute.For<ICartRepository>();
    private readonly ICatalogServiceClient _catalogClient = Substitute.For<ICatalogServiceClient>();
    private readonly IInventoryServiceClient _inventoryClient = Substitute.For<IInventoryServiceClient>();

    private AddItemCommandHandler CreateHandler() => new(_cartRepository, _catalogClient, _inventoryClient);

    private static VariantInfo BuildVariant(Guid variantId, bool isActive = true) =>
        new(variantId, Guid.NewGuid(), "Camiseta", "SKU-1", 20m, isActive);

    [Fact]
    public async Task Handle_ConStockSuficienteYCarritoNuevo_DeberiaCrearElCarritoYAgregarElItem()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var variant = BuildVariant(variantId);

        _cartRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((CartAggregate?)null);
        _catalogClient.GetVariantAsync(variantId, Arg.Any<CancellationToken>()).Returns(variant);
        _inventoryClient.GetAvailableQuantityAsync(variantId, "token-123", Arg.Any<CancellationToken>()).Returns(10);

        var handler = CreateHandler();
        var result = await handler.Handle(new AddItemCommand(userId, variantId, 3, "token-123"), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Quantity.Should().Be(3);
        result.Subtotal.Should().Be(60m);
        await _cartRepository.Received(1).AddAsync(Arg.Any<CartAggregate>(), Arg.Any<CancellationToken>());
        await _cartRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConStockInsuficiente_DeberiaLanzarInsufficientStockAppException()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var variant = BuildVariant(variantId);

        _cartRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((CartAggregate?)null);
        _catalogClient.GetVariantAsync(variantId, Arg.Any<CancellationToken>()).Returns(variant);
        _inventoryClient.GetAvailableQuantityAsync(variantId, "token-123", Arg.Any<CancellationToken>()).Returns(2);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new AddItemCommand(userId, variantId, 5, "token-123"), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockAppException>();
        await _cartRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConVarianteInexistente_DeberiaLanzarNotFoundAppException()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        _catalogClient.GetVariantAsync(variantId, Arg.Any<CancellationToken>()).Returns((VariantInfo?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new AddItemCommand(userId, variantId, 1, "token-123"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Handle_ConVarianteInactiva_DeberiaLanzarNotFoundAppException()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var variant = BuildVariant(variantId, isActive: false);

        _catalogClient.GetVariantAsync(variantId, Arg.Any<CancellationToken>()).Returns(variant);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new AddItemCommand(userId, variantId, 1, "token-123"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Handle_ConCarritoExistenteYVarianteYaEnElCarrito_DeberiaValidarLaSumaTotalContraElStock()
    {
        var userId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var variant = BuildVariant(variantId);

        var existingCart = CartAggregate.CreateForUser(userId);
        existingCart.AddItem(variantId, variant.ProductId, variant.ProductName, variant.Sku, variant.Price, 4); // ya hay 4

        _cartRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existingCart);
        _catalogClient.GetVariantAsync(variantId, Arg.Any<CancellationToken>()).Returns(variant);
        // Solo quedan 5 disponibles: 4 ya reservadas en el carrito + 3 más pedidas = 7, debería fallar.
        _inventoryClient.GetAvailableQuantityAsync(variantId, "token-123", Arg.Any<CancellationToken>()).Returns(5);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(new AddItemCommand(userId, variantId, 3, "token-123"), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockAppException>();
    }
}

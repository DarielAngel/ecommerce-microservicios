using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using Ecommerce.Cart.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Cart.UnitTests;

public class CartTests
{
    private static (Guid variantId, Guid productId) NewIds() => (Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void AddItem_ConVarianteNueva_DeberiaCrearLineaYDevolverElItemNuevo()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        var (variantId, productId) = NewIds();

        var newItem = cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 20m, 2);

        newItem.Should().NotBeNull();
        cart.Items.Should().ContainSingle();
        cart.Items.First().Quantity.Should().Be(2);
        cart.Subtotal.Should().Be(40m);
    }

    [Fact]
    public void AddItem_ConVarianteYaEnElCarrito_DeberiaSumarCantidadYNoDevolverItemNuevo()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        var (variantId, productId) = NewIds();

        cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 20m, 2);
        var secondCall = cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 999m, 3);

        secondCall.Should().BeNull("la segunda llamada solo incrementa una línea existente, no crea una nueva");
        cart.Items.Should().ContainSingle();
        cart.Items.First().Quantity.Should().Be(5);
        // El precio se queda congelado en el de la PRIMERA vez que se agregó (20), no el de la segunda llamada (999).
        cart.Items.First().UnitPrice.Should().Be(20m);
        cart.Subtotal.Should().Be(100m);
    }

    [Fact]
    public void AddItem_ConCantidadCeroONegativa_DeberiaLanzarDomainException()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        var (variantId, productId) = NewIds();

        var act = () => cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 20m, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetItemQuantity_ConCero_DeberiaEliminarLaLinea()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        var (variantId, productId) = NewIds();
        cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 20m, 2);

        cart.SetItemQuantity(variantId, 0);

        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void SetItemQuantity_SobreVarianteQueNoEsta_DeberiaLanzarDomainException()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());

        var act = () => cart.SetItemQuantity(Guid.NewGuid(), 5);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RemoveItem_DeberiaQuitarLaLineaYActualizarSubtotal()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        var (variantId, productId) = NewIds();
        cart.AddItem(variantId, productId, "Camiseta", "SKU-1", 20m, 2);

        cart.RemoveItem(variantId);

        cart.Items.Should().BeEmpty();
        cart.Subtotal.Should().Be(0m);
    }

    [Fact]
    public void Clear_DeberiaVaciarTodasLasLineas()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "A", "SKU-A", 10m, 1);
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "B", "SKU-B", 5m, 3);

        cart.Clear();

        cart.Items.Should().BeEmpty();
        cart.Subtotal.Should().Be(0m);
    }

    [Fact]
    public void Subtotal_ConVariasLineas_DeberiaSumarCorrectamente()
    {
        var cart = CartAggregate.CreateForUser(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "A", "SKU-A", 10m, 2); // 20
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "B", "SKU-B", 5m, 3);  // 15

        cart.Subtotal.Should().Be(35m);
        cart.TotalItemCount.Should().Be(5);
    }
}

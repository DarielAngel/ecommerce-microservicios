using Ecommerce.Wishlist.Domain.Entities;
using Ecommerce.Wishlist.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Wishlist.UnitTests;

public class WishlistItemTests
{
    [Fact]
    public void Create_ConDatosValidos_DeberiaGuardarUsuarioProductoYFechaUtc()
    {
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var item = WishlistItem.Create(userId, productId);

        item.UserId.Should().Be(userId);
        item.ProductId.Should().Be(productId);
        item.AddedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        item.AddedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_SinUsuario_DeberiaLanzarDomainException()
    {
        var act = () => WishlistItem.Create(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("El usuario es obligatorio.");
    }

    [Fact]
    public void Create_SinProducto_DeberiaLanzarDomainException()
    {
        var act = () => WishlistItem.Create(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("El producto es obligatorio.");
    }
}

using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Application.Features;
using Ecommerce.Wishlist.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Wishlist.UnitTests;

public class AddToWishlistCommandHandlerTests
{
    private readonly IWishlistRepository _wishlist = Substitute.For<IWishlistRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    private Task Handle() =>
        new AddToWishlistCommandHandler(_wishlist).Handle(new AddToWishlistCommand(_userId, _productId), CancellationToken.None);

    [Fact]
    public async Task Handle_ProductoNuevo_DeberiaAgregarloConElUsuarioYProductoCorrectos()
    {
        _wishlist.ExistsAsync(_userId, _productId, Arg.Any<CancellationToken>()).Returns(false);
        _wishlist.CountAsync(_userId, Arg.Any<CancellationToken>()).Returns(3);

        await Handle();

        await _wishlist.Received(1).AddIfMissingAsync(
            Arg.Is<WishlistItem>(i => i.UserId == _userId && i.ProductId == _productId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProductoQueYaEsFavorito_NoDeberiaVolverAAgregarlo()
    {
        _wishlist.ExistsAsync(_userId, _productId, Arg.Any<CancellationToken>()).Returns(true);

        await Handle();

        await _wishlist.DidNotReceiveWithAnyArgs().AddIfMissingAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ProductoQueYaEsFavorito_ConLaListaLlena_NoDeberiaFallar()
    {
        // Repetir el clic en un corazón ya marcado no es "agregar uno más": no debe chocar con el tope.
        _wishlist.ExistsAsync(_userId, _productId, Arg.Any<CancellationToken>()).Returns(true);
        _wishlist.CountAsync(_userId, Arg.Any<CancellationToken>()).Returns(WishlistItem.MaxItemsPerUser);

        var act = Handle;

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ProductoNuevo_ConLaListaLlena_DeberiaLanzarConflictoSinAgregar()
    {
        _wishlist.ExistsAsync(_userId, _productId, Arg.Any<CancellationToken>()).Returns(false);
        _wishlist.CountAsync(_userId, Arg.Any<CancellationToken>()).Returns(WishlistItem.MaxItemsPerUser);

        var act = Handle;

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*llena*");
        await _wishlist.DidNotReceiveWithAnyArgs().AddIfMissingAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ProductoNuevo_JustoDebajoDelTope_DeberiaAgregarlo()
    {
        _wishlist.ExistsAsync(_userId, _productId, Arg.Any<CancellationToken>()).Returns(false);
        _wishlist.CountAsync(_userId, Arg.Any<CancellationToken>()).Returns(WishlistItem.MaxItemsPerUser - 1);

        await Handle();

        await _wishlist.Received(1).AddIfMissingAsync(Arg.Any<WishlistItem>(), Arg.Any<CancellationToken>());
    }
}

public class RemoveFromWishlistCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeberiaQuitarElFavoritoDelUsuario()
    {
        var wishlist = Substitute.For<IWishlistRepository>();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await new RemoveFromWishlistCommandHandler(wishlist)
            .Handle(new RemoveFromWishlistCommand(userId, productId), CancellationToken.None);

        await wishlist.Received(1).RemoveAsync(userId, productId, Arg.Any<CancellationToken>());
    }
}

public class GetMyWishlistQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeberiaDevolverLosFavoritosEnElOrdenDelRepositorio()
    {
        var wishlist = Substitute.For<IWishlistRepository>();
        var userId = Guid.NewGuid();
        var newer = WishlistItem.Create(userId, Guid.NewGuid());
        var older = WishlistItem.Create(userId, Guid.NewGuid());
        wishlist.ListAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<WishlistItem> { newer, older });

        var result = await new GetMyWishlistQueryHandler(wishlist).Handle(new GetMyWishlistQuery(userId), CancellationToken.None);

        result.Select(r => r.ProductId).Should().Equal(newer.ProductId, older.ProductId);
        result[0].AddedAtUtc.Should().Be(newer.AddedAtUtc);
    }

    [Fact]
    public async Task Handle_SinFavoritos_DeberiaDevolverListaVacia()
    {
        var wishlist = Substitute.For<IWishlistRepository>();
        wishlist.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<WishlistItem>());

        var result = await new GetMyWishlistQueryHandler(wishlist).Handle(new GetMyWishlistQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeEmpty();
    }
}

using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Application.Features;
using Ecommerce.Reviews.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class CreateReviewCommandHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();
    private readonly IVerifiedPurchaseRepository _purchases = Substitute.For<IVerifiedPurchaseRepository>();

    private CreateReviewCommandHandler CreateHandler() => new(_reviews, _purchases);

    private static CreateReviewCommand Command(Guid productId, Guid userId) =>
        new(productId, userId, "Ana Pérez Gómez", 5, "Excelente", "Me encantó");

    [Fact]
    public async Task Handle_ConDatosValidos_DeberiaGuardarYDevolverLaReseñaConNombreProtegido()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _reviews.GetByUserAndProductAsync(userId, productId, Arg.Any<CancellationToken>()).Returns((Review?)null);

        var result = await CreateHandler().Handle(Command(productId, userId), CancellationToken.None);

        result.ProductId.Should().Be(productId);
        result.Rating.Should().Be(5);
        result.Title.Should().Be("Excelente");
        result.AuthorName.Should().Be("Ana P.", "nunca se expone el nombre completo del cliente");
        await _reviews.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
        await _reviews.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SiElUsuarioComproElProducto_DeberiaMarcarCompraVerificada()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _reviews.GetByUserAndProductAsync(userId, productId, Arg.Any<CancellationToken>()).Returns((Review?)null);
        _purchases.HasPurchasedAsync(userId, productId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(Command(productId, userId), CancellationToken.None);

        result.IsVerifiedPurchase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SiElUsuarioNoComproElProducto_NoDeberiaMarcarCompraVerificada()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _reviews.GetByUserAndProductAsync(userId, productId, Arg.Any<CancellationToken>()).Returns((Review?)null);
        _purchases.HasPurchasedAsync(userId, productId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(Command(productId, userId), CancellationToken.None);

        result.IsVerifiedPurchase.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SiYaExisteUnaReseñaDelUsuario_DeberiaLanzarConflictoSinGuardarNada()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existing = Review.Create(productId, userId, "Ana P.", 4, "Ya opiné", null, false);
        _reviews.GetByUserAndProductAsync(userId, productId, Arg.Any<CancellationToken>()).Returns(existing);

        var act = () => CreateHandler().Handle(Command(productId, userId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>().WithMessage("*Ya reseñaste*");
        await _reviews.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
        await _reviews.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

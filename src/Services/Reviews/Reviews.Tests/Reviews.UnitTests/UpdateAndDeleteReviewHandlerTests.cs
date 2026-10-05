using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Application.Features;
using Ecommerce.Reviews.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class UpdateReviewCommandHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    private static Review ReviewOf(Guid userId) => Review.Create(Guid.NewGuid(), userId, "Ana P.", 2, "Regular", "Meh", false);

    [Fact]
    public async Task Handle_ElAutor_DeberiaEditarYGuardar()
    {
        var userId = Guid.NewGuid();
        var review = ReviewOf(userId);
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await new UpdateReviewCommandHandler(_reviews)
            .Handle(new UpdateReviewCommand(review.Id, userId, 5, "Mejoró", "Ahora sí"), CancellationToken.None);

        result.Rating.Should().Be(5);
        result.Title.Should().Be("Mejoró");
        await _reviews.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtroUsuario_DeberiaLanzarForbiddenSinModificar()
    {
        var review = ReviewOf(Guid.NewGuid());
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var act = () => new UpdateReviewCommandHandler(_reviews)
            .Handle(new UpdateReviewCommand(review.Id, Guid.NewGuid(), 5, "Hackeo", "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
        review.Title.Should().Be("Regular");
        await _reviews.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SiNoExiste_DeberiaLanzarNotFound()
    {
        _reviews.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Review?)null);

        var act = () => new UpdateReviewCommandHandler(_reviews)
            .Handle(new UpdateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, "t", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

public class DeleteReviewCommandHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    private static Review ReviewOf(Guid userId) => Review.Create(Guid.NewGuid(), userId, "Ana P.", 2, "Regular", "Meh", false);

    [Fact]
    public async Task Handle_ElAutor_DeberiaEliminar()
    {
        var userId = Guid.NewGuid();
        var review = ReviewOf(userId);
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        await new DeleteReviewCommandHandler(_reviews)
            .Handle(new DeleteReviewCommand(review.Id, userId, RequesterIsAdmin: false), CancellationToken.None);

        _reviews.Received(1).Remove(review);
        await _reviews.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnAdminPuedeEliminarCualquierReseña_Moderacion()
    {
        var review = ReviewOf(Guid.NewGuid());
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        await new DeleteReviewCommandHandler(_reviews)
            .Handle(new DeleteReviewCommand(review.Id, Guid.NewGuid(), RequesterIsAdmin: true), CancellationToken.None);

        _reviews.Received(1).Remove(review);
    }

    [Fact]
    public async Task Handle_OtroUsuarioSinSerAdmin_DeberiaLanzarForbidden()
    {
        var review = ReviewOf(Guid.NewGuid());
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var act = () => new DeleteReviewCommandHandler(_reviews)
            .Handle(new DeleteReviewCommand(review.Id, Guid.NewGuid(), RequesterIsAdmin: false), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
        _reviews.DidNotReceive().Remove(Arg.Any<Review>());
    }

    [Fact]
    public async Task Handle_SiNoExiste_DeberiaLanzarNotFound()
    {
        _reviews.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Review?)null);

        var act = () => new DeleteReviewCommandHandler(_reviews)
            .Handle(new DeleteReviewCommand(Guid.NewGuid(), Guid.NewGuid(), true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

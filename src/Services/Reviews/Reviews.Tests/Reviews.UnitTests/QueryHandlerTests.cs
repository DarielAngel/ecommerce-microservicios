using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Application.Features;
using Ecommerce.Reviews.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class ListProductReviewsQueryHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    [Theory]
    [InlineData(null, ReviewSort.Newest)]
    [InlineData("", ReviewSort.Newest)]
    [InlineData("newest", ReviewSort.Newest)]
    [InlineData("highest", ReviewSort.Highest)]
    [InlineData("lowest", ReviewSort.Lowest)]
    public async Task Handle_DeberiaTraducirElOrdenSolicitado(string? sort, ReviewSort expected)
    {
        var productId = Guid.NewGuid();
        _reviews.ListByProductAsync(productId, expected, 1, 10, Arg.Any<CancellationToken>())
            .Returns(new ReviewPage(new List<Review>(), 0));

        await new ListProductReviewsQueryHandler(_reviews)
            .Handle(new ListProductReviewsQuery(productId, sort), CancellationToken.None);

        await _reviews.Received(1).ListByProductAsync(productId, expected, 1, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeberiaMapearLasReseñasYCalcularLasPaginas()
    {
        var productId = Guid.NewGuid();
        var review = Review.Create(productId, Guid.NewGuid(), "Ana P.", 4, "Bien", "Ok", true);
        _reviews.ListByProductAsync(productId, ReviewSort.Newest, 1, 2, Arg.Any<CancellationToken>())
            .Returns(new ReviewPage(new List<Review> { review }, 5));

        var result = await new ListProductReviewsQueryHandler(_reviews)
            .Handle(new ListProductReviewsQuery(productId, null, 1, 2), CancellationToken.None);

        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Should().Match<ReviewResult>(r =>
            r.Title == "Bien" && r.AuthorName == "Ana P." && r.IsVerifiedPurchase);
    }
}

public class RatingSummaryQueryHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    [Fact]
    public async Task GetRatingSummary_DeberiaCalcularPromedioYConteoDesdeLaDistribucion()
    {
        var productId = Guid.NewGuid();
        _reviews.GetRatingDistributionAsync(productId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, int> { [5] = 1, [3] = 1 });

        var result = await new GetRatingSummaryQueryHandler(_reviews)
            .Handle(new GetRatingSummaryQuery(productId), CancellationToken.None);

        result.Count.Should().Be(2);
        result.Average.Should().Be(4.0);
        result.Distribution[5].Should().Be(1);
        result.Distribution[4].Should().Be(0);
    }

    [Fact]
    public async Task GetRatingSummaries_DeberiaDevolverUnaEntradaPorCadaProductoPedido_AunSinReseñas()
    {
        var conReseñas = Guid.NewGuid();
        var sinReseñas = Guid.NewGuid();
        _reviews.GetRatingDistributionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, IReadOnlyDictionary<int, int>>
            {
                [conReseñas] = new Dictionary<int, int> { [4] = 2 }
            });

        var result = await new GetRatingSummariesQueryHandler(_reviews)
            .Handle(new GetRatingSummariesQuery(new[] { conReseñas, sinReseñas }), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Single(r => r.ProductId == conReseñas).Count.Should().Be(2);
        result.Single(r => r.ProductId == sinReseñas).Count.Should().Be(0);
        result.Single(r => r.ProductId == sinReseñas).Average.Should().Be(0);
    }

    [Fact]
    public async Task GetRatingSummaries_ConListaVacia_NoDeberiaConsultarLaBaseDeDatos()
    {
        var result = await new GetRatingSummariesQueryHandler(_reviews)
            .Handle(new GetRatingSummariesQuery(Array.Empty<Guid>()), CancellationToken.None);

        result.Should().BeEmpty();
        await _reviews.DidNotReceive().GetRatingDistributionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}

public class GetMyReviewQueryHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    [Fact]
    public async Task Handle_SiExiste_DeberiaDevolverLaReseñaDelUsuario()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var review = Review.Create(productId, userId, "Ana P.", 4, "Mía", null, false);
        _reviews.GetByUserAndProductAsync(userId, productId, Arg.Any<CancellationToken>()).Returns(review);

        var result = await new GetMyReviewQueryHandler(_reviews)
            .Handle(new GetMyReviewQuery(productId, userId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Mía");
    }

    [Fact]
    public async Task Handle_SiNoExiste_DeberiaDevolverNull()
    {
        _reviews.GetByUserAndProductAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Review?)null);

        var result = await new GetMyReviewQueryHandler(_reviews)
            .Handle(new GetMyReviewQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}

public class RecordVerifiedPurchaseCommandHandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();
    private readonly IVerifiedPurchaseRepository _purchases = Substitute.For<IVerifiedPurchaseRepository>();

    [Fact]
    public async Task Handle_DeberiaRegistrarLaCompraYVerificarLasReseñasPrevias()
    {
        var userId = Guid.NewGuid();
        var products = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await new RecordVerifiedPurchaseCommandHandler(_purchases, _reviews)
            .Handle(new RecordVerifiedPurchaseCommand(userId, products), CancellationToken.None);

        await _purchases.Received(1).RecordAsync(userId, Arg.Is<IReadOnlyCollection<Guid>>(p => p.SequenceEqual(products)), Arg.Any<CancellationToken>());
        await _reviews.Received(1).MarkVerifiedAsync(userId, Arg.Is<IReadOnlyCollection<Guid>>(p => p.SequenceEqual(products)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinProductos_NoDeberiaHacerNada()
    {
        await new RecordVerifiedPurchaseCommandHandler(_purchases, _reviews)
            .Handle(new RecordVerifiedPurchaseCommand(Guid.NewGuid(), Array.Empty<Guid>()), CancellationToken.None);

        await _purchases.DidNotReceive().RecordAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _reviews.DidNotReceive().MarkVerifiedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}

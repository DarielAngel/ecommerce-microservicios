using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Application.Features;
using Ecommerce.Reviews.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class ListReviewsForModerationQueryTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    [Fact]
    public async Task Handle_PasaLosFiltrosLimpiosYMapeaLaPagina()
    {
        var productId = Guid.NewGuid();
        var review = Review.Create(productId, Guid.NewGuid(), "Ana P.", 1, "Malo", "Llegó roto", false);
        _reviews.ListForModerationAsync(Arg.Any<ReviewModerationFilter>(), 2, 10, Arg.Any<CancellationToken>())
            .Returns(new ReviewPage(new List<Review> { review }, 11));

        var result = await new ListReviewsForModerationQueryHandler(_reviews)
            .Handle(new ListReviewsForModerationQuery(1, "  roto  ", productId, 2, 10), CancellationToken.None);

        await _reviews.Received(1).ListForModerationAsync(
            new ReviewModerationFilter(1, "roto", productId), 2, 10, Arg.Any<CancellationToken>());
        result.TotalPages.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("Malo");
    }

    [Fact]
    public async Task Handle_UnaBusquedaEnBlanco_NoFiltra()
    {
        _reviews.ListForModerationAsync(Arg.Any<ReviewModerationFilter>(), 1, 20, Arg.Any<CancellationToken>())
            .Returns(new ReviewPage(new List<Review>(), 0));

        await new ListReviewsForModerationQueryHandler(_reviews)
            .Handle(new ListReviewsForModerationQuery(Search: "   "), CancellationToken.None);

        await _reviews.Received(1).ListForModerationAsync(new ReviewModerationFilter(), 1, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, null, 1, 20, false)]
    [InlineData(6, null, 1, 20, false)]
    [InlineData(null, null, 0, 20, false)]
    [InlineData(null, null, 1, 51, false)]
    [InlineData(3, "rotura", 1, 50, true)]
    [InlineData(null, null, 1, 20, true)]
    public void Validator_ReglasDeFiltrosYPaginas(int? rating, string? search, int page, int pageSize, bool valid)
    {
        var result = new ListReviewsForModerationQueryValidator()
            .Validate(new ListReviewsForModerationQuery(rating, search, null, page, pageSize));

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_BusquedaDemasiadoLarga_EsInvalida()
    {
        new ListReviewsForModerationQueryValidator()
            .Validate(new ListReviewsForModerationQuery(Search: new string('a', 101)))
            .IsValid.Should().BeFalse();
    }
}

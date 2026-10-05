using Ecommerce.Reviews.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class CreateReviewCommandValidatorTests
{
    private readonly CreateReviewCommandValidator _validator = new();

    private static CreateReviewCommand Valid(int rating = 5, string title = "Genial", string? comment = "Muy bueno") =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Ana Pérez", rating, title, comment);

    [Fact]
    public void ConDatosValidos_DeberiaSerValido() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void ConCalificacionFueraDeRango_DeberiaSerInvalido(int rating) =>
        _validator.Validate(Valid(rating: rating)).IsValid.Should().BeFalse();

    [Fact]
    public void SinTitulo_DeberiaSerInvalido() => _validator.Validate(Valid(title: "")).IsValid.Should().BeFalse();

    [Fact]
    public void ConTituloMuyLargo_DeberiaSerInvalido() =>
        _validator.Validate(Valid(title: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void ConComentarioMuyLargo_DeberiaSerInvalido() =>
        _validator.Validate(Valid(comment: new string('a', 2001))).IsValid.Should().BeFalse();

    [Fact]
    public void SinComentario_DeberiaSerValido() => _validator.Validate(Valid(comment: null)).IsValid.Should().BeTrue();

    [Fact]
    public void ConProductoVacio_DeberiaSerInvalido() =>
        _validator.Validate(new CreateReviewCommand(Guid.Empty, Guid.NewGuid(), "Ana", 5, "t", null)).IsValid.Should().BeFalse();
}

public class UpdateReviewCommandValidatorTests
{
    private readonly UpdateReviewCommandValidator _validator = new();

    [Fact]
    public void ConDatosValidos_DeberiaSerValido() =>
        _validator.Validate(new UpdateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 4, "Bien", "Ok")).IsValid.Should().BeTrue();

    [Fact]
    public void ConCalificacionInvalida_DeberiaSerInvalido() =>
        _validator.Validate(new UpdateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 7, "Bien", "Ok")).IsValid.Should().BeFalse();
}

public class ListProductReviewsQueryValidatorTests
{
    private readonly ListProductReviewsQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("newest")]
    [InlineData("highest")]
    [InlineData("lowest")]
    public void ConOrdenValido_DeberiaSerValido(string? sort) =>
        _validator.Validate(new ListProductReviewsQuery(Guid.NewGuid(), sort)).IsValid.Should().BeTrue();

    [Fact]
    public void ConOrdenInvalido_DeberiaSerInvalido() =>
        _validator.Validate(new ListProductReviewsQuery(Guid.NewGuid(), "al-azar")).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void ConPaginacionInvalida_DeberiaSerInvalido(int page, int pageSize) =>
        _validator.Validate(new ListProductReviewsQuery(Guid.NewGuid(), null, page, pageSize)).IsValid.Should().BeFalse();
}

public class GetRatingSummariesQueryValidatorTests
{
    private readonly GetRatingSummariesQueryValidator _validator = new();

    [Fact]
    public void ConHastaCienProductos_DeberiaSerValido() =>
        _validator.Validate(new GetRatingSummariesQuery(Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList())).IsValid.Should().BeTrue();

    [Fact]
    public void ConMasDeCienProductos_DeberiaSerInvalido() =>
        _validator.Validate(new GetRatingSummariesQuery(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList())).IsValid.Should().BeFalse();
}

using Ecommerce.Reviews.Domain.Entities;
using Ecommerce.Reviews.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class ReviewTests
{
    private static Review Valid(int rating = 5, string title = "Excelente", string? comment = "Me encantó", bool verified = false) =>
        Review.Create(Guid.NewGuid(), Guid.NewGuid(), "Ana P.", rating, title, comment, verified);

    [Fact]
    public void Create_ConDatosValidos_DeberiaInicializarLaReseña()
    {
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var review = Review.Create(productId, userId, "Ana P.", 4, "Muy bueno", "Cumple lo prometido", isVerifiedPurchase: true);

        review.Id.Should().NotBeEmpty();
        review.ProductId.Should().Be(productId);
        review.UserId.Should().Be(userId);
        review.AuthorName.Should().Be("Ana P.");
        review.Rating.Should().Be(4);
        review.Title.Should().Be("Muy bueno");
        review.Comment.Should().Be("Cumple lo prometido");
        review.IsVerifiedPurchase.Should().BeTrue();
        review.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        review.UpdatedAtUtc.Should().Be(review.CreatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_ConCalificacionFueraDeRango_DeberiaLanzarDomainException(int rating)
    {
        var act = () => Valid(rating: rating);
        act.Should().Throw<DomainException>().WithMessage("*calificación*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Create_ConCalificacionEnLosExtremos_DeberiaSerValida(int rating)
    {
        Valid(rating: rating).Rating.Should().Be(rating);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SinTitulo_DeberiaLanzarDomainException(string title)
    {
        var act = () => Valid(title: title);
        act.Should().Throw<DomainException>().WithMessage("*título*");
    }

    [Fact]
    public void Create_ConTituloMuyLargo_DeberiaLanzarDomainException()
    {
        var act = () => Valid(title: new string('a', Review.MaxTitleLength + 1));
        act.Should().Throw<DomainException>().WithMessage("*título*");
    }

    [Fact]
    public void Create_ConComentarioMuyLargo_DeberiaLanzarDomainException()
    {
        var act = () => Valid(comment: new string('a', Review.MaxCommentLength + 1));
        act.Should().Throw<DomainException>().WithMessage("*comentario*");
    }

    [Fact]
    public void Create_SinComentario_DeberiaGuardarCadenaVacia()
    {
        Valid(comment: null).Comment.Should().BeEmpty();
    }

    [Fact]
    public void Create_DeberiaRecortarLosEspaciosDeTituloYComentario()
    {
        var review = Valid(title: "  Hola  ", comment: "  mundo \n");

        review.Title.Should().Be("Hola");
        review.Comment.Should().Be("mundo");
    }

    [Fact]
    public void Create_ConProductoOUsuarioVacio_DeberiaLanzarDomainException()
    {
        var sinProducto = () => Review.Create(Guid.Empty, Guid.NewGuid(), "Ana P.", 5, "t", null, false);
        var sinUsuario = () => Review.Create(Guid.NewGuid(), Guid.Empty, "Ana P.", 5, "t", null, false);

        sinProducto.Should().Throw<DomainException>();
        sinUsuario.Should().Throw<DomainException>();
    }

    [Fact]
    public void Edit_DeberiaActualizarLosCamposYSoloLaFechaDeActualizacion()
    {
        var review = Valid(rating: 2, title: "Regular", comment: "Esperaba más");
        var created = review.CreatedAtUtc;

        review.Edit(5, "Mejoró", "Lo arreglaron");

        review.Rating.Should().Be(5);
        review.Title.Should().Be("Mejoró");
        review.Comment.Should().Be("Lo arreglaron");
        review.CreatedAtUtc.Should().Be(created);
        review.UpdatedAtUtc.Should().BeOnOrAfter(created);
    }

    [Fact]
    public void Edit_ConDatosInvalidos_NoDeberiaModificarLaReseña()
    {
        var review = Valid(rating: 3, title: "Normal", comment: "Ok");

        var act = () => review.Edit(9, "Nuevo", "Nuevo");

        act.Should().Throw<DomainException>();
        review.Rating.Should().Be(3);
        review.Title.Should().Be("Normal");
    }

    [Fact]
    public void MarkAsVerifiedPurchase_DeberiaMarcarla()
    {
        var review = Valid(verified: false);

        review.MarkAsVerifiedPurchase();

        review.IsVerifiedPurchase.Should().BeTrue();
    }

    [Theory]
    [InlineData("Ana Pérez Gómez", "Ana P.")]
    [InlineData("Juan Carlos Ruiz", "Juan C.")]
    [InlineData("mariela prueba", "Mariela P.")]
    [InlineData("Ana", "Ana")]
    [InlineData("   Luis    Mora  ", "Luis M.")]
    [InlineData("", "Cliente")]
    [InlineData("   ", "Cliente")]
    [InlineData(null, "Cliente")]
    public void ToDisplayName_DeberiaMostrarSoloNombreEInicial_ProtegiendoLaPrivacidad(string? fullName, string expected)
    {
        Review.ToDisplayName(fullName).Should().Be(expected);
    }
}

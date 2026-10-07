using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Features;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Catalog.UnitTests;

public class SuggestProductsQueryHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private Task<IReadOnlyList<ProductSummary>> Handle(string? term, int limit = 6) =>
        new SuggestProductsQueryHandler(_products).Handle(new SuggestProductsQuery(term, limit), CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task ConMenosDeDosLetras_NoConsultaLaBase(string? term)
    {
        (await Handle(term)).Should().BeEmpty();
        await _products.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
    }

    [Fact]
    public async Task ConTextoValido_ConsultaSinEspaciosYConElLimitePedido()
    {
        var expected = new List<ProductSummary> { new(Guid.NewGuid(), "Galaxy S24", "galaxy-s24", Guid.NewGuid(), "Celulares", 799, null, true) };
        _products.SuggestAsync("gal", 4, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await Handle("  gal ", 4);

        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Validador_LimitaLaCantidadDeSugerencias(int limit, bool valid)
    {
        new SuggestProductsQueryValidator().Validate(new SuggestProductsQuery("gal", limit)).IsValid.Should().Be(valid);
    }
}

public class GetRelatedProductsQueryHandlerTests
{
    [Fact]
    public async Task DelegaEnElRepositorioConElProductoYElLimite()
    {
        var products = Substitute.For<IProductRepository>();
        var productId = Guid.NewGuid();
        products.GetRelatedAsync(productId, 5, Arg.Any<CancellationToken>()).Returns(Array.Empty<ProductSummary>());

        await new GetRelatedProductsQueryHandler(products).Handle(new GetRelatedProductsQuery(productId, 5), CancellationToken.None);

        await products.Received(1).GetRelatedAsync(productId, 5, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(8, true)]
    [InlineData(21, false)]
    public void Validador_LimitaLaCantidad(int limit, bool valid)
    {
        new GetRelatedProductsQueryValidator().Validate(new GetRelatedProductsQuery(Guid.NewGuid(), limit)).IsValid.Should().Be(valid);
    }
}

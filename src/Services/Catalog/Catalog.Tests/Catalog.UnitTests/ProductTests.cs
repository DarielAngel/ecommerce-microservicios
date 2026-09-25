using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Catalog.UnitTests;

public class ProductTests
{
    private static Product BuildProduct() =>
        Product.Create("Camiseta básica", "Camiseta 100% algodón", Guid.NewGuid());

    [Fact]
    public void AddVariant_ConDatosValidos_DeberiaAgregarseAlProducto()
    {
        var product = BuildProduct();

        var variant = product.AddVariant("CAM-M-ROJO", 19.99m, new Dictionary<string, string>
        {
            ["Talla"] = "M",
            ["Color"] = "Rojo"
        });

        product.Variants.Should().ContainSingle();
        variant.Sku.Should().Be("CAM-M-ROJO");
        variant.Price.Should().Be(19.99m);
    }

    [Fact]
    public void AddVariant_ConSkuDuplicadoEnElMismoProducto_DeberiaLanzarDomainException()
    {
        var product = BuildProduct();
        product.AddVariant("CAM-M-ROJO", 19.99m, new Dictionary<string, string>());

        var act = () => product.AddVariant("cam-m-rojo", 21.99m, new Dictionary<string, string>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddVariant_ConPrecioNegativoOCero_DeberiaLanzarDomainException()
    {
        var product = BuildProduct();

        var act = () => product.AddVariant("CAM-L-AZUL", 0m, new Dictionary<string, string>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddImage_LaPrimeraImagen_DeberiaQuedarComoPrincipalAunqueNoSeIndique()
    {
        var product = BuildProduct();

        var image = product.AddImage("foto1.jpg", isPrimary: false);

        image.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void AddImage_UnaNuevaComoPrincipal_DeberiaDesmarcarLasAnteriores()
    {
        var product = BuildProduct();
        var primera = product.AddImage("foto1.jpg", isPrimary: true);
        var segunda = product.AddImage("foto2.jpg", isPrimary: true);

        primera.IsPrimary.Should().BeFalse();
        segunda.IsPrimary.Should().BeTrue();
        product.Images.Count(i => i.IsPrimary).Should().Be(1);
    }
}

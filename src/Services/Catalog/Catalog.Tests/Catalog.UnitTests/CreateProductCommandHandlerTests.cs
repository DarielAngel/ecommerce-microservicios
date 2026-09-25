using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Features;
using Ecommerce.Catalog.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Catalog.UnitTests;

public class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();

    private CreateProductCommandHandler CreateHandler() => new(_productRepository, _categoryRepository);

    [Fact]
    public async Task Handle_ConCategoriaExistenteYSkuNuevo_DeberiaCrearElProducto()
    {
        var categoryId = Guid.NewGuid();
        _categoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Category.Create("Ropa"));
        _productRepository.ExistsBySkuAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new CreateProductCommand(
            "Camiseta básica", "Descripción", categoryId,
            new List<VariantInput> { new("CAM-M-ROJO", 19.99m, new Dictionary<string, string> { ["Talla"] = "M" }) });

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Name.Should().Be("Camiseta básica");
        result.Variants.Should().ContainSingle(v => v.Sku == "CAM-M-ROJO");
        await _productRepository.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConCategoriaInexistente_DeberiaLanzarNotFoundAppException()
    {
        var categoryId = Guid.NewGuid();
        _categoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var command = new CreateProductCommand(
            "Producto", "Descripción", categoryId,
            new List<VariantInput> { new("SKU-1", 10m, new Dictionary<string, string>()) });

        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task Handle_ConSkuYaExistente_DeberiaLanzarConflictAppException()
    {
        var categoryId = Guid.NewGuid();
        _categoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Category.Create("Ropa"));
        _productRepository.ExistsBySkuAsync("SKU-REPETIDO", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateProductCommand(
            "Producto", "Descripción", categoryId,
            new List<VariantInput> { new("SKU-REPETIDO", 10m, new Dictionary<string, string>()) });

        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
    }
}

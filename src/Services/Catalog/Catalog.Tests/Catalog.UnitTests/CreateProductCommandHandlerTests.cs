using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Features;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Contracts.Events;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Catalog.UnitTests;

public class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();

    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();

    private CreateProductCommandHandler CreateHandler() => new(_productRepository, _categoryRepository, _eventPublisher);

    private Task NingunEventoPublicado() =>
        _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<VariantCreatedEvent>(), Arg.Any<CancellationToken>());

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
        await NingunEventoPublicado();
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
        await NingunEventoPublicado();
    }

    [Fact]
    public async Task Handle_ConVariasVariantes_DeberiaPublicarUnVariantCreatedEventPorCadaUna()
    {
        // Inventario crea su registro de stock a partir de este evento: sin él, la variante existe pero no tiene stock.
        var categoryId = Guid.NewGuid();
        _categoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns(Category.Create("Ropa"));
        _productRepository.ExistsBySkuAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var published = new List<VariantCreatedEvent>();
        await _eventPublisher.PublishAsync(Arg.Do<VariantCreatedEvent>(e => published.Add(e)), Arg.Any<CancellationToken>());

        var command = new CreateProductCommand(
            "Camiseta", "Descripción", categoryId,
            new List<VariantInput>
            {
                new("SKU-A", 10m, new Dictionary<string, string> { ["Talla"] = "S" }),
                new("SKU-B", 12m, new Dictionary<string, string> { ["Talla"] = "M" })
            });

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        published.Should().HaveCount(2);
        published.Select(e => e.Sku).Should().BeEquivalentTo(new[] { "SKU-A", "SKU-B" });
        published.Should().OnlyContain(e => e.ProductId == result.Id);
        published.Select(e => e.VariantId).Should().BeEquivalentTo(result.Variants.Select(v => v.Id));
    }

    [Fact]
    public async Task Handle_SiElGuardadoFalla_NoDeberiaPublicarEventos_ParaNoCrearStockDeVariantesInexistentes()
    {
        var categoryId = Guid.NewGuid();
        _categoryRepository.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns(Category.Create("Ropa"));
        _productRepository.ExistsBySkuAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _productRepository.When(r => r.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("base de datos caída"));

        var command = new CreateProductCommand(
            "Producto", "Descripción", categoryId,
            new List<VariantInput> { new("SKU-1", 10m, new Dictionary<string, string>()) });

        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await NingunEventoPublicado();
    }
}

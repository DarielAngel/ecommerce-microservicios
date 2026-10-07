using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Features;
using Ecommerce.Inventory.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Inventory.UnitTests;

public class GetAvailabilityQueryHandlerTests
{
    [Theory]
    [InlineData(0, "OutOfStock", 0)]
    [InlineData(-2, "OutOfStock", 0)]
    [InlineData(1, "LowStock", 1)]
    [InlineData(5, "LowStock", 5)]
    [InlineData(6, "InStock", null)]
    [InlineData(500, "InStock", null)]
    public void From_ClasificaConElStockDisponibleReal(int available, string status, int? left)
    {
        var result = GetAvailabilityQueryHandler.From(Guid.NewGuid(), available);

        result.Status.Should().Be(status);
        result.QuantityLeft.Should().Be(left, "con stock de sobra no se expone la cantidad exacta");
    }

    [Fact]
    public async Task Handle_RespetaElOrden_SinRepetir_YLoQueNoExisteSaleAgotado()
    {
        // Que se descuenten las reservas se prueba en integración, con el UPDATE real de Inventario.
        var repository = Substitute.For<IStockItemRepository>();
        var plenty = StockItem.CreateEmpty(Guid.NewGuid());
        plenty.SetQuantityOnHand(40);
        var almostGone = StockItem.CreateEmpty(Guid.NewGuid());
        almostGone.SetQuantityOnHand(3);
        var unknown = Guid.NewGuid();
        repository.GetByVariantIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<StockItem> { almostGone, plenty });

        var result = await new GetAvailabilityQueryHandler(repository).Handle(
            new GetAvailabilityQuery(new[] { plenty.VariantId, almostGone.VariantId, unknown, plenty.VariantId }), CancellationToken.None);

        result.Select(r => r.VariantId).Should().Equal(plenty.VariantId, almostGone.VariantId, unknown);
        result[0].Status.Should().Be("InStock");
        result[1].Should().BeEquivalentTo(new VariantAvailability(almostGone.VariantId, "LowStock", 3));
        result[2].Status.Should().Be("OutOfStock");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validador_ExigeEntre1Y100Variantes(int count, bool valid)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        new GetAvailabilityQueryValidator().Validate(new GetAvailabilityQuery(ids)).IsValid.Should().Be(valid);
    }
}

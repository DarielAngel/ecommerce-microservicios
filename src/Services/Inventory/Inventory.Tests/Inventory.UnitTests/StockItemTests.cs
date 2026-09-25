using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Inventory.UnitTests;

public class StockItemTests
{
    [Fact]
    public void CreateEmpty_DeberiaEmpezarEnCero()
    {
        var variantId = Guid.NewGuid();

        var stockItem = StockItem.CreateEmpty(variantId);

        stockItem.VariantId.Should().Be(variantId);
        stockItem.QuantityOnHand.Should().Be(0);
        stockItem.QuantityReserved.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    public void SetQuantityOnHand_ConValorValido_DeberiaActualizar()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());

        stockItem.SetQuantityOnHand(50);

        stockItem.QuantityOnHand.Should().Be(50);
        stockItem.QuantityAvailable.Should().Be(50);
    }

    [Fact]
    public void SetQuantityOnHand_Negativo_DeberiaLanzarDomainException()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());

        var act = () => stockItem.SetQuantityOnHand(-1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void IsLowStock_ConDisponibleMenorOIgualAlUmbral_DeberiaSerTrue()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());
        stockItem.SetLowStockThreshold(5);
        stockItem.SetQuantityOnHand(5);

        stockItem.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public void IsLowStock_ConDisponibleMayorAlUmbral_DeberiaSerFalse()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());
        stockItem.SetLowStockThreshold(5);
        stockItem.SetQuantityOnHand(20);

        stockItem.IsLowStock.Should().BeFalse();
    }
}

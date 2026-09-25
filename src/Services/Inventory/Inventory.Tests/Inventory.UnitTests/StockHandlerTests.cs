using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Features;
using Ecommerce.Inventory.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Inventory.UnitTests;

public class AdjustStockCommandHandlerTests
{
    private readonly IStockItemRepository _repository = Substitute.For<IStockItemRepository>();

    [Fact]
    public async Task Handle_ConVarianteExistente_DeberiaActualizarYDevolverResultado()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());
        _repository.GetByVariantIdAsync(stockItem.VariantId, Arg.Any<CancellationToken>()).Returns(stockItem);

        var handler = new AdjustStockCommandHandler(_repository);
        var result = await handler.Handle(new AdjustStockCommand(stockItem.VariantId, 100), CancellationToken.None);

        result.QuantityOnHand.Should().Be(100);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConVarianteInexistente_DeberiaLanzarNotFoundAppException()
    {
        _repository.GetByVariantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((StockItem?)null);

        var handler = new AdjustStockCommandHandler(_repository);
        var act = async () => await handler.Handle(new AdjustStockCommand(Guid.NewGuid(), 10), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

public class GetStockQueryHandlerTests
{
    private readonly IStockItemRepository _repository = Substitute.For<IStockItemRepository>();

    [Fact]
    public async Task Handle_ConVarianteExistente_DeberiaDevolverElStock()
    {
        var stockItem = StockItem.CreateEmpty(Guid.NewGuid());
        stockItem.SetQuantityOnHand(30);
        _repository.GetByVariantIdAsync(stockItem.VariantId, Arg.Any<CancellationToken>()).Returns(stockItem);

        var handler = new GetStockQueryHandler(_repository);
        var result = await handler.Handle(new GetStockQuery(stockItem.VariantId), CancellationToken.None);

        result.QuantityOnHand.Should().Be(30);
        result.QuantityAvailable.Should().Be(30);
    }

    [Fact]
    public async Task Handle_ConVarianteInexistente_DeberiaLanzarNotFoundAppException()
    {
        _repository.GetByVariantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((StockItem?)null);

        var handler = new GetStockQueryHandler(_repository);
        var act = async () => await handler.Handle(new GetStockQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }
}

using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Features;
using Ecommerce.Inventory.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Ecommerce.Inventory.UnitTests;

public class CreateStockItemCommandHandlerTests
{
    private readonly IStockItemRepository _repository = Substitute.For<IStockItemRepository>();

    [Fact]
    public async Task Handle_ConVarianteNueva_DeberiaCrearElRegistro()
    {
        _repository.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateStockItemCommandHandler(_repository);
        await handler.Handle(new CreateStockItemCommand(Guid.NewGuid()), CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<StockItem>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConVarianteQueYaTieneRegistro_NoDeberiaDuplicar()
    {
        // Simula un reintento/redelivery del mismo mensaje de RabbitMQ.
        _repository.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateStockItemCommandHandler(_repository);
        await handler.Handle(new CreateStockItemCommand(Guid.NewGuid()), CancellationToken.None);

        await _repository.DidNotReceive().AddAsync(Arg.Any<StockItem>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

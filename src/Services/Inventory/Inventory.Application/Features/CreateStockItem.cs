using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Domain.Entities;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record CreateStockItemCommand(Guid VariantId) : IRequest;

public class CreateStockItemCommandHandler : IRequestHandler<CreateStockItemCommand>
{
    private readonly IStockItemRepository _stockItemRepository;

    public CreateStockItemCommandHandler(IStockItemRepository stockItemRepository)
    {
        _stockItemRepository = stockItemRepository;
    }

    public async Task Handle(CreateStockItemCommand request, CancellationToken ct)
    {
        // Idempotente a propósito: si el consumidor de RabbitMQ reprocesa el mismo mensaje
        // (reintentos, redelivery), no debe fallar ni duplicar el registro.
        if (await _stockItemRepository.ExistsAsync(request.VariantId, ct))
        {
            return;
        }

        var stockItem = StockItem.CreateEmpty(request.VariantId);
        await _stockItemRepository.AddAsync(stockItem, ct);
        await _stockItemRepository.SaveChangesAsync(ct);
    }
}

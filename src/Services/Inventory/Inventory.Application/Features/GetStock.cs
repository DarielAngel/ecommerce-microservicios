using Ecommerce.Inventory.Application.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record GetStockQuery(Guid VariantId) : IRequest<StockResult>;

public record StockResult(
    Guid VariantId,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime UpdatedAtUtc);

public class GetStockQueryHandler : IRequestHandler<GetStockQuery, StockResult>
{
    private readonly IStockItemRepository _stockItemRepository;

    public GetStockQueryHandler(IStockItemRepository stockItemRepository)
    {
        _stockItemRepository = stockItemRepository;
    }

    public async Task<StockResult> Handle(GetStockQuery request, CancellationToken ct)
    {
        var stockItem = await _stockItemRepository.GetByVariantIdAsync(request.VariantId, ct)
            ?? throw new NotFoundAppException(
                "No hay registro de stock para esa variante. ¿Seguro que el ID es correcto y la variante existe en Catálogo?");

        return new StockResult(
            stockItem.VariantId,
            stockItem.QuantityOnHand,
            stockItem.QuantityReserved,
            stockItem.QuantityAvailable,
            stockItem.LowStockThreshold,
            stockItem.IsLowStock,
            stockItem.UpdatedAtUtc);
    }
}

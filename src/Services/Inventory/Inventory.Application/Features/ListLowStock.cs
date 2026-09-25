using Ecommerce.Inventory.Application.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record ListLowStockQuery : IRequest<IReadOnlyList<StockResult>>;

public class ListLowStockQueryHandler : IRequestHandler<ListLowStockQuery, IReadOnlyList<StockResult>>
{
    private readonly IStockItemRepository _stockItemRepository;

    public ListLowStockQueryHandler(IStockItemRepository stockItemRepository)
    {
        _stockItemRepository = stockItemRepository;
    }

    public async Task<IReadOnlyList<StockResult>> Handle(ListLowStockQuery request, CancellationToken ct)
    {
        var items = await _stockItemRepository.ListLowStockAsync(ct);

        return items.Select(stockItem => new StockResult(
            stockItem.VariantId,
            stockItem.QuantityOnHand,
            stockItem.QuantityReserved,
            stockItem.QuantityAvailable,
            stockItem.LowStockThreshold,
            stockItem.IsLowStock,
            stockItem.UpdatedAtUtc)).ToList();
    }
}

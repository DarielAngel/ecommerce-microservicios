using Ecommerce.Inventory.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Features;

/// <summary>
/// Las unidades de una devolución ya reembolsada vuelven al stock (Fase 7). Idempotente por devolución y variante:
/// RabbitMQ puede entregar el mismo evento más de una vez.
/// </summary>
public record RestockReturnedItemsCommand(Guid ReturnId, IReadOnlyList<(Guid VariantId, int Quantity)> Items) : IRequest<int>;

public class RestockReturnedItemsCommandHandler : IRequestHandler<RestockReturnedItemsCommand, int>
{
    private readonly IStockItemRepository _stock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RestockReturnedItemsCommandHandler> _logger;

    public RestockReturnedItemsCommandHandler(
        IStockItemRepository stock, IUnitOfWork unitOfWork, ILogger<RestockReturnedItemsCommandHandler> logger)
    {
        _stock = stock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <returns>Cuántas variantes se repusieron (0 si el evento ya se había procesado).</returns>
    public async Task<int> Handle(RestockReturnedItemsCommand request, CancellationToken ct)
    {
        var items = request.Items
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.VariantId)
            .Select(g => (VariantId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        // Todas las variantes de la devolución o ninguna: si algo falla a la mitad, el reintento empieza de cero.
        var restocked = await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var count = 0;
            foreach (var (variantId, quantity) in items)
            {
                if (await _stock.RestockReturnedAsync(request.ReturnId, variantId, quantity, token)) count++;
            }
            return count;
        }, ct);

        _logger.LogInformation("Devolución {ReturnId}: {Count} variante(s) repuesta(s) al stock.", request.ReturnId, restocked);
        return restocked;
    }
}

using Ecommerce.Inventory.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record AdjustStockCommand(Guid VariantId, int NewQuantityOnHand) : IRequest<StockResult>;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.NewQuantityOnHand).GreaterThanOrEqualTo(0);
    }
}

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, StockResult>
{
    private readonly IStockItemRepository _stockItemRepository;

    public AdjustStockCommandHandler(IStockItemRepository stockItemRepository)
    {
        _stockItemRepository = stockItemRepository;
    }

    public async Task<StockResult> Handle(AdjustStockCommand request, CancellationToken ct)
    {
        var stockItem = await _stockItemRepository.GetByVariantIdAsync(request.VariantId, ct)
            ?? throw new NotFoundAppException(
                "No hay registro de stock para esa variante todavía (espera a que Catálogo publique el evento, o revisa que el VariantId sea correcto).");

        stockItem.SetQuantityOnHand(request.NewQuantityOnHand);
        await _stockItemRepository.SaveChangesAsync(ct);

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

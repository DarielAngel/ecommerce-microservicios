using Ecommerce.Inventory.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

/// <summary>
/// Disponibilidad tal como la ve el cliente en la tienda (T4.4). A propósito NO expone el stock exacto
/// cuando hay de sobra ni las reservas: solo "hay", "quedan N" (pocas) o "agotado". Siempre se calcula
/// con el stock disponible real (físico menos reservado); nunca se inventa escasez para presionar.
/// </summary>
public record VariantAvailability(Guid VariantId, string Status, int? QuantityLeft);

public static class AvailabilityStatus
{
    public const string InStock = "InStock";
    public const string LowStock = "LowStock";
    public const string OutOfStock = "OutOfStock";

    /// <summary>
    /// "Quedan pocas" = 5 o menos disponibles. Es un umbral fijo para el cliente, distinto del
    /// LowStockThreshold de cada ítem, que es un aviso interno de reposición para el Admin.
    /// </summary>
    public const int LowStockLimit = 5;
}

public record GetAvailabilityQuery(IReadOnlyList<Guid> VariantIds) : IRequest<IReadOnlyList<VariantAvailability>>;

public class GetAvailabilityQueryValidator : AbstractValidator<GetAvailabilityQuery>
{
    public const int MaxVariants = 100;

    public GetAvailabilityQueryValidator()
    {
        RuleFor(x => x.VariantIds).NotEmpty().WithMessage("Indica al menos una variante.")
            .Must(ids => ids.Count <= MaxVariants).WithMessage($"Se pueden consultar hasta {MaxVariants} variantes a la vez.");
    }
}

public class GetAvailabilityQueryHandler : IRequestHandler<GetAvailabilityQuery, IReadOnlyList<VariantAvailability>>
{
    private readonly IStockItemRepository _stock;

    public GetAvailabilityQueryHandler(IStockItemRepository stock) => _stock = stock;

    public async Task<IReadOnlyList<VariantAvailability>> Handle(GetAvailabilityQuery request, CancellationToken ct)
    {
        var ids = request.VariantIds.Distinct().ToList();
        var items = (await _stock.GetByVariantIdsAsync(ids, ct)).ToDictionary(s => s.VariantId);

        // En el orden pedido. Una variante sin registro de stock todavía no se puede comprar
        // (Inventario aún no procesó su alta): se informa como agotada.
        return ids.Select(id => items.TryGetValue(id, out var item) ? From(item.VariantId, item.QuantityAvailable)
                                                                     : From(id, 0)).ToList();
    }

    public static VariantAvailability From(Guid variantId, int available) => available switch
    {
        <= 0 => new(variantId, AvailabilityStatus.OutOfStock, 0),
        <= AvailabilityStatus.LowStockLimit => new(variantId, AvailabilityStatus.LowStock, available),
        _ => new(variantId, AvailabilityStatus.InStock, null)
    };
}

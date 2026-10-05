using Ecommerce.Reviews.Application.Common;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

/// <summary>Lo dispara el consumidor del evento OrderPaid: registra qué productos compró el usuario.</summary>
public record RecordVerifiedPurchaseCommand(Guid UserId, IReadOnlyCollection<Guid> ProductIds) : IRequest;

public class RecordVerifiedPurchaseCommandHandler : IRequestHandler<RecordVerifiedPurchaseCommand>
{
    private readonly IVerifiedPurchaseRepository _purchases;
    private readonly IReviewRepository _reviews;

    public RecordVerifiedPurchaseCommandHandler(IVerifiedPurchaseRepository purchases, IReviewRepository reviews)
    {
        _purchases = purchases;
        _reviews = reviews;
    }

    public async Task Handle(RecordVerifiedPurchaseCommand request, CancellationToken ct)
    {
        if (request.ProductIds.Count == 0) return; // evento anterior a este campo: nada que registrar

        var productIds = request.ProductIds.Distinct().ToList();

        await _purchases.RecordAsync(request.UserId, productIds, ct);

        // Si el cliente ya había reseñado antes de comprar, su reseña pasa a ser "compra verificada".
        await _reviews.MarkVerifiedAsync(request.UserId, productIds, ct);
    }
}

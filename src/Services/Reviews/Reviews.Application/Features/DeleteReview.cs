using Ecommerce.Reviews.Application.Common;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

/// <summary>El autor puede borrar la suya; un Admin puede borrar cualquiera (moderación).</summary>
public record DeleteReviewCommand(Guid ReviewId, Guid RequesterId, bool RequesterIsAdmin) : IRequest;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand>
{
    private readonly IReviewRepository _reviews;

    public DeleteReviewCommandHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task Handle(DeleteReviewCommand request, CancellationToken ct)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, ct)
            ?? throw new NotFoundAppException("La reseña no existe.");

        if (!request.RequesterIsAdmin && review.UserId != request.RequesterId)
            throw new ForbiddenAppException("Solo puedes eliminar tus propias reseñas.");

        _reviews.Remove(review);
        await _reviews.SaveChangesAsync(ct);
    }
}

using Ecommerce.Reviews.Application.Common;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

public record GetMyReviewQuery(Guid ProductId, Guid UserId) : IRequest<ReviewResult?>;

public class GetMyReviewQueryHandler : IRequestHandler<GetMyReviewQuery, ReviewResult?>
{
    private readonly IReviewRepository _reviews;

    public GetMyReviewQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<ReviewResult?> Handle(GetMyReviewQuery request, CancellationToken ct)
    {
        var review = await _reviews.GetByUserAndProductAsync(request.UserId, request.ProductId, ct);
        return review is null ? null : ReviewResult.From(review);
    }
}

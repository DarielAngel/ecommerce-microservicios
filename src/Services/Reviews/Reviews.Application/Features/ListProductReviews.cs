using Ecommerce.Reviews.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

public record ListProductReviewsQuery(Guid ProductId, string? Sort = null, int Page = 1, int PageSize = 10)
    : IRequest<PagedResult<ReviewResult>>;

public class ListProductReviewsQueryValidator : AbstractValidator<ListProductReviewsQuery>
{
    private static readonly string[] ValidSorts = ["newest", "highest", "lowest"];

    public ListProductReviewsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Sort)
            .Must(s => ValidSorts.Contains(s!.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.Sort))
            .WithMessage($"sort debe ser uno de: {string.Join(", ", ValidSorts)}.");
    }
}

public class ListProductReviewsQueryHandler : IRequestHandler<ListProductReviewsQuery, PagedResult<ReviewResult>>
{
    private readonly IReviewRepository _reviews;

    public ListProductReviewsQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<PagedResult<ReviewResult>> Handle(ListProductReviewsQuery request, CancellationToken ct)
    {
        var sort = request.Sort?.ToLowerInvariant() switch
        {
            "highest" => ReviewSort.Highest,
            "lowest" => ReviewSort.Lowest,
            _ => ReviewSort.Newest
        };

        var page = await _reviews.ListByProductAsync(request.ProductId, sort, request.Page, request.PageSize, ct);

        return new PagedResult<ReviewResult>(
            page.Items.Select(ReviewResult.From).ToList(), request.Page, request.PageSize, page.TotalCount);
    }
}

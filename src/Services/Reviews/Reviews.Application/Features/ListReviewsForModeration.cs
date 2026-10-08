using Ecommerce.Reviews.Application.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

/// <summary>Moderación (solo Admin): todas las reseñas, las más nuevas primero, con filtros opcionales.</summary>
public record ListReviewsForModerationQuery(
    int? Rating = null, string? Search = null, Guid? ProductId = null, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ReviewResult>>;

public class ListReviewsForModerationQueryValidator : AbstractValidator<ListReviewsForModerationQuery>
{
    public ListReviewsForModerationQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Rating!.Value)
            .InclusiveBetween(Domain.Entities.Review.MinRating, Domain.Entities.Review.MaxRating)
            .When(x => x.Rating.HasValue)
            .OverridePropertyName(nameof(ListReviewsForModerationQuery.Rating))
            .WithMessage("Las estrellas van de 1 a 5.");
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("La búsqueda admite hasta 100 caracteres.");
    }
}

public class ListReviewsForModerationQueryHandler
    : IRequestHandler<ListReviewsForModerationQuery, PagedResult<ReviewResult>>
{
    private readonly IReviewRepository _reviews;

    public ListReviewsForModerationQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<PagedResult<ReviewResult>> Handle(ListReviewsForModerationQuery request, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = await _reviews.ListForModerationAsync(
            new ReviewModerationFilter(request.Rating, search, request.ProductId), request.Page, request.PageSize, ct);

        return new PagedResult<ReviewResult>(
            page.Items.Select(ReviewResult.From).ToList(), request.Page, request.PageSize, page.TotalCount);
    }
}

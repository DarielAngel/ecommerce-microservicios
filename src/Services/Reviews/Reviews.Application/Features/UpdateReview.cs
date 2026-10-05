using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

public record UpdateReviewCommand(Guid ReviewId, Guid UserId, int Rating, string Title, string? Comment) : IRequest<ReviewResult>;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating)
            .WithMessage($"La calificación debe estar entre {Review.MinRating} y {Review.MaxRating}.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(Review.MaxTitleLength).WithMessage($"El título no puede superar {Review.MaxTitleLength} caracteres.");
        RuleFor(x => x.Comment).MaximumLength(Review.MaxCommentLength)
            .WithMessage($"El comentario no puede superar {Review.MaxCommentLength} caracteres.");
    }
}

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, ReviewResult>
{
    private readonly IReviewRepository _reviews;

    public UpdateReviewCommandHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<ReviewResult> Handle(UpdateReviewCommand request, CancellationToken ct)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, ct)
            ?? throw new NotFoundAppException("La reseña no existe.");

        if (review.UserId != request.UserId)
            throw new ForbiddenAppException("Solo puedes editar tus propias reseñas.");

        review.Edit(request.Rating, request.Title, request.Comment);
        await _reviews.SaveChangesAsync(ct);

        return ReviewResult.From(review);
    }
}

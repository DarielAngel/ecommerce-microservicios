using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Reviews.Application.Features;

public record CreateReviewCommand(
    Guid ProductId, Guid UserId, string AuthorFullName, int Rating, string Title, string? Comment) : IRequest<ReviewResult>;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating)
            .WithMessage($"La calificación debe estar entre {Review.MinRating} y {Review.MaxRating}.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(Review.MaxTitleLength).WithMessage($"El título no puede superar {Review.MaxTitleLength} caracteres.");
        RuleFor(x => x.Comment).MaximumLength(Review.MaxCommentLength)
            .WithMessage($"El comentario no puede superar {Review.MaxCommentLength} caracteres.");
    }
}

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, ReviewResult>
{
    private readonly IReviewRepository _reviews;
    private readonly IVerifiedPurchaseRepository _purchases;

    public CreateReviewCommandHandler(IReviewRepository reviews, IVerifiedPurchaseRepository purchases)
    {
        _reviews = reviews;
        _purchases = purchases;
    }

    public async Task<ReviewResult> Handle(CreateReviewCommand request, CancellationToken ct)
    {
        var existing = await _reviews.GetByUserAndProductAsync(request.UserId, request.ProductId, ct);
        if (existing is not null)
            throw new ConflictAppException("Ya reseñaste este producto. Edita tu reseña existente.");

        var verified = await _purchases.HasPurchasedAsync(request.UserId, request.ProductId, ct);

        var review = Review.Create(
            request.ProductId, request.UserId, Review.ToDisplayName(request.AuthorFullName),
            request.Rating, request.Title, request.Comment, verified);

        await _reviews.AddAsync(review, ct);
        // La verificación de arriba es una optimización de UX; la garantía real contra duplicados
        // es el índice UNIQUE, cuya violación el repositorio traduce a ConflictAppException.
        await _reviews.SaveChangesAsync(ct);

        return ReviewResult.From(review);
    }
}

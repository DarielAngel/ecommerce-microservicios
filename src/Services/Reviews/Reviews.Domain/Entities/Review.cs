using Ecommerce.Reviews.Domain.Exceptions;

namespace Ecommerce.Reviews.Domain.Entities;

/// <summary>
/// Reseña de un producto escrita por un cliente. Regla central: una reseña por usuario y producto
/// (la garantiza además un índice UNIQUE en la base de datos, que es la defensa real ante carreras).
/// </summary>
public class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxTitleLength = 100;
    public const int MaxCommentLength = 2000;

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Nombre para mostrar ya protegido (ej. "Ana P."): nunca guardamos el nombre completo aquí.</summary>
    public string AuthorName { get; private set; } = null!;

    public int Rating { get; private set; }
    public string Title { get; private set; } = null!;
    public string Comment { get; private set; } = string.Empty;
    public bool IsVerifiedPurchase { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Review() { }

    public static Review Create(
        Guid productId, Guid userId, string authorName, int rating, string title, string? comment, bool isVerifiedPurchase)
    {
        if (productId == Guid.Empty) throw new DomainException("El producto es obligatorio.");
        if (userId == Guid.Empty) throw new DomainException("El usuario es obligatorio.");

        var (cleanTitle, cleanComment) = Validate(rating, title, comment);
        var now = DateTime.UtcNow;

        return new Review
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Cliente" : authorName.Trim(),
            Rating = rating,
            Title = cleanTitle,
            Comment = cleanComment,
            IsVerifiedPurchase = isVerifiedPurchase,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>Valida primero y recién entonces asigna: si algo es inválido, la reseña queda intacta.</summary>
    public void Edit(int rating, string title, string? comment)
    {
        var (cleanTitle, cleanComment) = Validate(rating, title, comment);

        Rating = rating;
        Title = cleanTitle;
        Comment = cleanComment;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsVerifiedPurchase() => IsVerifiedPurchase = true;

    /// <summary>
    /// "Ana Pérez Gómez" -> "Ana P." — nombre y la inicial del segundo término. Protege la privacidad del
    /// cliente en una API pública sin perder el toque humano de la reseña.
    /// </summary>
    public static string ToDisplayName(string? fullName)
    {
        var parts = (fullName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0) return "Cliente";

        var first = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
        return parts.Length == 1 ? first : $"{first} {char.ToUpperInvariant(parts[1][0])}.";
    }

    private static (string Title, string Comment) Validate(int rating, string? title, string? comment)
    {
        if (rating < MinRating || rating > MaxRating)
            throw new DomainException($"La calificación debe estar entre {MinRating} y {MaxRating}.");

        var cleanTitle = (title ?? string.Empty).Trim();
        if (cleanTitle.Length == 0)
            throw new DomainException("El título es obligatorio.");
        if (cleanTitle.Length > MaxTitleLength)
            throw new DomainException($"El título no puede superar {MaxTitleLength} caracteres.");

        var cleanComment = (comment ?? string.Empty).Trim();
        if (cleanComment.Length > MaxCommentLength)
            throw new DomainException($"El comentario no puede superar {MaxCommentLength} caracteres.");

        return (cleanTitle, cleanComment);
    }
}

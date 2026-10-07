using FluentValidation.Results;

namespace Ecommerce.Wishlist.Application.Common;

public class ValidationAppException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IEnumerable<ValidationFailure> failures) : base("Se encontraron errores de validación.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
    }
}

/// <summary>La operación choca con el estado actual (ej. la lista de favoritos ya está llena).</summary>
public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}

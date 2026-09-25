using FluentValidation.Results;

namespace Ecommerce.Cart.Application.Common;

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

public class NotFoundAppException : Exception
{
    public NotFoundAppException(string message) : base(message) { }
}

/// <summary>No hay suficiente stock disponible para lo que se está pidiendo agregar/actualizar.</summary>
public class InsufficientStockAppException : Exception
{
    public InsufficientStockAppException(string message) : base(message) { }
}

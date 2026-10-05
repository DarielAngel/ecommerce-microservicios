using FluentValidation.Results;

namespace Ecommerce.Reviews.Application.Common;

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

/// <summary>La operación choca con el estado actual (ej. ya existe una reseña de este usuario para el producto).</summary>
public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}

/// <summary>El usuario está autenticado, pero no tiene permiso sobre este recurso (ej. editar la reseña de otro).</summary>
public class ForbiddenAppException : Exception
{
    public ForbiddenAppException(string message) : base(message) { }
}

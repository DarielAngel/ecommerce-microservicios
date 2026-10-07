using FluentValidation.Results;

namespace Ecommerce.Loyalty.Application.Common;

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

/// <summary>La operación choca con el estado actual (ej. no alcanzan los puntos).</summary>
public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}

/// <summary>Autenticado, pero sin permiso sobre este recurso (ej. tocar el canje de la orden de otro).</summary>
public class ForbiddenAppException : Exception
{
    public ForbiddenAppException(string message) : base(message) { }
}

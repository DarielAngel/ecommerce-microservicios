using FluentValidation.Results;

namespace Ecommerce.Users.Application.Common;

/// <summary>Error 400: datos de entrada inválidos (formato, campos requeridos, etc.)</summary>
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

/// <summary>Error 401: credenciales inválidas o token expirado/revocado.</summary>
public class UnauthorizedAppException : Exception
{
    public UnauthorizedAppException(string message) : base(message) { }
}

/// <summary>Error 409: el recurso ya existe (ej. email ya registrado).</summary>
public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}

/// <summary>Error 404: el recurso solicitado no existe.</summary>
public class NotFoundAppException : Exception
{
    public NotFoundAppException(string message) : base(message) { }
}

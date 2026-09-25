using FluentValidation.Results;

namespace Ecommerce.Payments.Application.Common;

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

public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message) { }
}

/// <summary>La comunicación con la API de PayPal falló (red, credenciales, respuesta inesperada).</summary>
public class PayPalCommunicationException : Exception
{
    public PayPalCommunicationException(string message, Exception? inner = null) : base(message, inner) { }
}

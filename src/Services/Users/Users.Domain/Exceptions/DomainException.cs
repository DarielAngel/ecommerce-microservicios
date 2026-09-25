namespace Ecommerce.Users.Domain.Exceptions;

/// <summary>
/// Excepción para violaciones de reglas de negocio del dominio.
/// No confundir con errores de validación de entrada (esos viven en Application).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

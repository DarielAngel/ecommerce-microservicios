namespace Ecommerce.Loyalty.Domain.Exceptions;

/// <summary>Violación de una regla de negocio del programa de puntos.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

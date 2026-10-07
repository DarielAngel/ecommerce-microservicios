namespace Ecommerce.Promotions.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>El cupón existe y es válido, pero no aplica a ESTA compra (vencido, compra mínima...).</summary>
public class CouponNotApplicableException : DomainException
{
    public CouponNotApplicableException(string message) : base(message) { }
}

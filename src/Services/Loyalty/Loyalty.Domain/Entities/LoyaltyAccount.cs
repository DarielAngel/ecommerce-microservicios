namespace Ecommerce.Loyalty.Domain.Entities;

/// <summary>
/// Una fila por cliente. No guarda el saldo (se calcula de los movimientos): existe para poder
/// BLOQUEARLA (SELECT ... FOR UPDATE) mientras se aparta un canje, así dos checkouts simultáneos
/// del mismo cliente no gastan los mismos puntos dos veces.
/// </summary>
public class LoyaltyAccount
{
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LoyaltyAccount() { }

    public static LoyaltyAccount Open(Guid userId, DateTime nowUtc) => new() { UserId = userId, CreatedAtUtc = nowUtc };
}

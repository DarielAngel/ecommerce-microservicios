namespace Ecommerce.Users.Domain.Entities;

/// <summary>
/// Representa un refresh token emitido a un usuario. Se guarda en base de datos
/// (nunca en el JWT mismo) para poder revocarlo y rotarlo de forma segura.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static RefreshToken Create(Guid userId, string tokenHash, TimeSpan lifetime) =>
        new(userId, tokenHash, DateTime.UtcNow.Add(lifetime));

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}

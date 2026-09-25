using Ecommerce.Users.Domain.Entities;

namespace Ecommerce.Users.Application.Common;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string hash);
}

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);

    /// <summary>Genera un refresh token opaco (no JWT) que se guarda hasheado en BD.</summary>
    string GenerateRefreshToken();

    string Hash(string token);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

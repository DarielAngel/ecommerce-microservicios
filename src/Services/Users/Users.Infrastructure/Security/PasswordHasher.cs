using Ecommerce.Users.Application.Common;

namespace Ecommerce.Users.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    // Work factor 12: buen balance seguridad/rendimiento en 2026 para hashing en el hilo de request.
    private const int WorkFactor = 12;

    public string Hash(string plainPassword) =>
        BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);

    public bool Verify(string plainPassword, string hash) =>
        BCrypt.Net.BCrypt.Verify(plainPassword, hash);
}

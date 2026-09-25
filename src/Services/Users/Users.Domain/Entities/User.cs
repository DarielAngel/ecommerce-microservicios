using Ecommerce.Users.Domain.Enums;
using Ecommerce.Users.Domain.Exceptions;
using Ecommerce.Users.Domain.ValueObjects;

namespace Ecommerce.Users.Domain.Entities;

/// <summary>
/// Raíz de agregado. La contraseña nunca se maneja en texto plano aquí:
/// el dominio solo conoce el hash (calculado en Infrastructure vía IPasswordHasher
/// y pasado ya calculado al crear el usuario).
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // EF Core necesita un constructor sin parámetros (privado, no se usa desde el código de dominio).
    private User() { }

    private User(Guid id, Email email, string passwordHash, string fullName, UserRole role)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static User CreateCustomer(Email email, string passwordHash, string fullName)
    {
        ValidateFullName(fullName);
        return new User(Guid.NewGuid(), email, passwordHash, fullName.Trim(), UserRole.Cliente);
    }

    public static User CreateAdmin(Email email, string passwordHash, string fullName)
    {
        ValidateFullName(fullName);
        return new User(Guid.NewGuid(), email, passwordHash, fullName.Trim(), UserRole.Admin);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
        {
            throw new DomainException("El nombre completo debe tener al menos 2 caracteres.");
        }
    }
}

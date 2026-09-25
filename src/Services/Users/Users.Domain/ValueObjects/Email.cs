using System.Text.RegularExpressions;
using Ecommerce.Users.Domain.Exceptions;

namespace Ecommerce.Users.Domain.ValueObjects;

/// <summary>
/// Value Object: garantiza que en todo el sistema, un Email siempre sea válido.
/// Es inmutable y se compara por valor, no por referencia.
/// </summary>
public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El email no puede estar vacío.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalized))
        {
            throw new DomainException($"'{value}' no tiene un formato de email válido.");
        }

        return new Email(normalized);
    }

    public bool Equals(Email? other) =>
        other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(Email? left, Email? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Email? left, Email? right) => !(left == right);
}

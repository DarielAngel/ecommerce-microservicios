using Ecommerce.Users.Domain.Exceptions;

namespace Ecommerce.Users.Domain.Entities;

/// <summary>
/// Una dirección de la libreta del cliente (Fase 5). Las reglas que involucran a TODAS las
/// direcciones del cliente (máximo, una sola predeterminada) viven en <see cref="AddressBook"/>.
/// </summary>
public class Address
{
    public const int MaxLabel = 40;
    public const int MaxName = 120;
    public const int MaxPhone = 30;
    public const int MaxLine = 200;
    public const int MaxCity = 100;
    public const int MaxPostalCode = 20;
    public const int MaxCountry = 60;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Nombre corto para reconocerla: "Casa", "Oficina"…</summary>
    public string Label { get; private set; } = null!;
    public string RecipientName { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string Street { get; private set; } = null!;

    /// <summary>Piso, departamento, referencias.</summary>
    public string? Details { get; private set; }
    public string City { get; private set; } = null!;
    public string? Region { get; private set; }
    public string? PostalCode { get; private set; }
    public string Country { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Address() { }

    internal Address(Guid userId, AddressData data, DateTime nowUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = nowUtc;
        Apply(data, nowUtc);
    }

    internal void Apply(AddressData data, DateTime nowUtc)
    {
        Label = Required(data.Label, "Nombre de la dirección", MaxLabel);
        RecipientName = Required(data.RecipientName, "Quién recibe", MaxName);
        Phone = Optional(data.Phone, "Teléfono", MaxPhone);
        Street = Required(data.Street, "Calle y número", MaxLine);
        Details = Optional(data.Details, "Detalles", MaxLine);
        City = Required(data.City, "Ciudad", MaxCity);
        Region = Optional(data.Region, "Provincia o estado", MaxCity);
        PostalCode = Optional(data.PostalCode, "Código postal", MaxPostalCode);
        Country = Required(data.Country, "País", MaxCountry);
        UpdatedAtUtc = nowUtc;
    }

    internal void SetDefault(bool value, DateTime nowUtc)
    {
        if (IsDefault == value) return;
        IsDefault = value;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// La dirección en una sola línea, tal como viaja a Órdenes (que la guarda como texto:
    /// si el cliente la edita después, sus pedidos anteriores no cambian).
    /// </summary>
    public string Format()
    {
        var parts = new List<string> { RecipientName, Street };
        if (Details is not null) parts.Add(Details);
        var cityLine = string.Join(" ", new[] { PostalCode, City }.Where(p => p is not null));
        parts.Add(cityLine);
        if (Region is not null && !string.Equals(Region, City, StringComparison.OrdinalIgnoreCase)) parts.Add(Region);
        parts.Add(Country);
        var text = string.Join(", ", parts);
        return Phone is null ? text : $"{text} · Tel. {Phone}";
    }

    private static string Required(string? value, string field, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"Completa el campo «{field}».");
        if (trimmed.Length > max)
            throw new DomainException($"«{field}» no puede superar {max} caracteres.");
        return trimmed;
    }

    private static string? Optional(string? value, string field, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max)
            throw new DomainException($"«{field}» no puede superar {max} caracteres.");
        return trimmed;
    }
}

/// <summary>Los datos que el cliente escribe en el formulario de dirección.</summary>
public record AddressData(
    string? Label, string? RecipientName, string? Phone, string? Street, string? Details,
    string? City, string? Region, string? PostalCode, string? Country);

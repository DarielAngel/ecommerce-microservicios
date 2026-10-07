using System.Text.RegularExpressions;
using Ecommerce.Promotions.Domain.Exceptions;

namespace Ecommerce.Promotions.Domain.Entities;

public enum DiscountType
{
    /// <summary>Un porcentaje del subtotal (ej. 15 = 15 %), con un tope opcional en dinero.</summary>
    Percentage = 0,

    /// <summary>Un monto fijo en dinero (ej. 10 = $10).</summary>
    FixedAmount = 1
}

/// <summary>
/// Cupón de descuento. Concentra TODAS las reglas de si un cupón aplica y cuánto descuenta, para que
/// la validación desde el checkout y el canje real (en la saga de Órdenes) nunca den resultados distintos.
/// </summary>
public class Coupon
{
    public const int MaxCodeLength = 30;
    public const int MaxDescriptionLength = 200;

    /// <summary>
    /// Tope del porcentaje. Un 100 % dejaría la orden en $0 y PayPal no cobra $0; además ningún
    /// cupón real regala la compra entera.
    /// </summary>
    public const decimal MaxPercentage = 90m;

    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9_-]{2,29}$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DiscountType Type { get; private set; }
    public decimal Value { get; private set; }

    /// <summary>Solo para porcentajes: el descuento nunca supera este monto (ej. 20 % hasta $50).</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    public decimal MinimumSubtotal { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }

    /// <summary>Usos totales permitidos entre todos los clientes. null = sin límite.</summary>
    public int? UsageLimit { get; private set; }

    public bool OncePerCustomer { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Coupon() { }

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static Coupon Create(
        string code, string description, DiscountType type, decimal value, decimal? maxDiscountAmount,
        decimal minimumSubtotal, DateTime? startsAtUtc, DateTime? endsAtUtc, int? usageLimit, bool oncePerCustomer)
    {
        var normalized = NormalizeCode(code);
        if (!CodePattern.IsMatch(normalized))
            throw new DomainException(
                "El código debe tener entre 3 y 30 caracteres: letras, números, guion o guion bajo (ej. VERANO25).");

        var now = DateTime.UtcNow;
        var coupon = new Coupon { Id = Guid.NewGuid(), Code = normalized, IsActive = true, CreatedAtUtc = now };
        coupon.Update(description, type, value, maxDiscountAmount, minimumSubtotal, startsAtUtc, endsAtUtc, usageLimit, oncePerCustomer, isActive: true);
        return coupon;
    }

    /// <summary>Valida todo primero y recién entonces asigna: si algo es inválido, el cupón queda intacto.</summary>
    public void Update(
        string description, DiscountType type, decimal value, decimal? maxDiscountAmount,
        decimal minimumSubtotal, DateTime? startsAtUtc, DateTime? endsAtUtc, int? usageLimit, bool oncePerCustomer, bool isActive)
    {
        var cleanDescription = (description ?? string.Empty).Trim();
        if (cleanDescription.Length == 0)
            throw new DomainException("La descripción es obligatoria (la ve el cliente al aplicar el cupón).");
        if (cleanDescription.Length > MaxDescriptionLength)
            throw new DomainException($"La descripción no puede superar {MaxDescriptionLength} caracteres.");

        if (!Enum.IsDefined(type))
            throw new DomainException("El tipo de descuento no es válido.");

        if (type == DiscountType.Percentage && (value <= 0 || value > MaxPercentage))
            throw new DomainException($"El porcentaje debe ser mayor que 0 y como máximo {MaxPercentage}.");
        if (type == DiscountType.FixedAmount && value <= 0)
            throw new DomainException("El monto del descuento debe ser mayor que 0.");

        if (maxDiscountAmount is not null && type != DiscountType.Percentage)
            throw new DomainException("El tope de descuento solo aplica a cupones de porcentaje.");
        if (maxDiscountAmount is <= 0)
            throw new DomainException("El tope de descuento debe ser mayor que 0.");

        if (minimumSubtotal < 0)
            throw new DomainException("La compra mínima no puede ser negativa.");

        if (startsAtUtc is not null && endsAtUtc is not null && endsAtUtc <= startsAtUtc)
            throw new DomainException("La fecha de fin debe ser posterior a la de inicio.");

        if (usageLimit is <= 0)
            throw new DomainException("El límite de usos debe ser al menos 1 (o vacío para usos ilimitados).");

        Description = cleanDescription;
        Type = type;
        Value = decimal.Round(value, 2);
        MaxDiscountAmount = maxDiscountAmount is null ? null : decimal.Round(maxDiscountAmount.Value, 2);
        MinimumSubtotal = decimal.Round(minimumSubtotal, 2);
        StartsAtUtc = ToUtc(startsAtUtc);
        EndsAtUtc = ToUtc(endsAtUtc);
        UsageLimit = usageLimit;
        OncePerCustomer = oncePerCustomer;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Calcula el descuento para un subtotal, o lanza CouponNotApplicableException con un mensaje que
    /// se le puede mostrar al cliente tal cual. No mira los usos: eso depende de la base de datos.
    /// </summary>
    public decimal CalculateDiscount(decimal subtotal, DateTime nowUtc)
    {
        if (!IsActive)
            throw new CouponNotApplicableException("Este cupón ya no está disponible.");
        if (StartsAtUtc is not null && nowUtc < StartsAtUtc)
            throw new CouponNotApplicableException($"Este cupón todavía no está vigente (empieza el {StartsAtUtc:dd/MM/yyyy}).");
        if (EndsAtUtc is not null && nowUtc >= EndsAtUtc)
            throw new CouponNotApplicableException("Este cupón ya venció.");
        if (subtotal <= 0)
            throw new CouponNotApplicableException("Agrega productos antes de aplicar un cupón.");
        if (subtotal < MinimumSubtotal)
            throw new CouponNotApplicableException(
                $"Este cupón requiere una compra mínima de ${MinimumSubtotal:0.00} (tu compra: ${subtotal:0.00}).");

        if (Type == DiscountType.FixedAmount)
        {
            // Un descuento fijo igual o mayor que la compra la dejaría en $0 (o negativa).
            if (Value >= subtotal)
                throw new CouponNotApplicableException(
                    $"Este cupón descuenta ${Value:0.00}: tu compra tiene que superar ese monto.");
            return Value;
        }

        var discount = decimal.Round(subtotal * Value / 100m, 2, MidpointRounding.AwayFromZero);
        return MaxDiscountAmount is null ? discount : Math.Min(discount, MaxDiscountAmount.Value);
    }

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
    };
}

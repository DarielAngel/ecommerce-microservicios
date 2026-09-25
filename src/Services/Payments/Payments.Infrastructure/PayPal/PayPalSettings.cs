namespace Ecommerce.Payments.Infrastructure.PayPal;

public class PayPalSettings
{
    public const string SectionName = "PayPal";

    /// <summary>Sandbox por defecto. En producción real esto cambia a https://api-m.paypal.com</summary>
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Id del webhook configurado en el dashboard de PayPal (para verificar firmas).</summary>
    public string WebhookId { get; set; } = string.Empty;

    /// <summary>A dónde vuelve el comprador tras aprobar el pago en PayPal.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:5000/api/payments/return";

    /// <summary>A dónde vuelve el comprador si cancela antes de aprobar.</summary>
    public string CancelUrl { get; set; } = "http://localhost:5000/api/payments/cancel";
}

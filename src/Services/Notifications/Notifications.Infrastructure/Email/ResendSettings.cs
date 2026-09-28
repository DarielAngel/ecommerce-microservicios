namespace Ecommerce.Notifications.Infrastructure.Email;

public class ResendSettings
{
    public const string SectionName = "Resend";

    public string BaseUrl { get; set; } = "https://api.resend.com/";

    /// <summary>API key de Resend (empieza con "re_"). Se define por variable de entorno, nunca en el repo.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Remitente. "onboarding@resend.dev" es el remitente de pruebas de Resend: funciona sin
    /// verificar dominio, pero SOLO puede enviar al email con el que te registraste en Resend.
    /// Para enviar a cualquier cliente hay que verificar un dominio propio en Resend.
    /// </summary>
    public string FromAddress { get; set; } = "onboarding@resend.dev";
}

namespace Ecommerce.Users.Api.Options;

public class AdminProvisioningOptions
{
    public const string SectionName = "AdminProvisioning";

    /// <summary>
    /// Clave secreta que debe enviarse en el header "X-Admin-Provisioning-Key"
    /// para poder crear un usuario Admin. Se define por variable de entorno
    /// en producción, nunca se versiona en appsettings.json real.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}

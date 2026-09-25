namespace Ecommerce.Users.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Ecommerce.Users";
    public string Audience { get; set; } = "Ecommerce.Clients";

    /// <summary>Estándar recomendado: access token de vida corta.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Estándar recomendado: refresh token de vida larga, rotado en cada uso.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}

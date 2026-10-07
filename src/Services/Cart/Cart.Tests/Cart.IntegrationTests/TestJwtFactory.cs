using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Cart.IntegrationTests;

public static class TestJwtFactory
{
    public static string CreateToken(Guid userId, string role = "Cliente", string? email = null, string? fullName = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        if (email is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        if (fullName is not null) claims.Add(new Claim(ClaimTypes.Name, fullName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CartApiFactory.TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: CartApiFactory.TestJwtIssuer,
            audience: CartApiFactory.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

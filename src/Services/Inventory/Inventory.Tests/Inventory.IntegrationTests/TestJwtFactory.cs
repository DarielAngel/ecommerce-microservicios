using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Inventory.IntegrationTests;

public static class TestJwtFactory
{
    public static string CreateToken(string role, Guid? userId = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, (userId ?? Guid.NewGuid()).ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(InventoryApiFactory.TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: InventoryApiFactory.TestJwtIssuer,
            audience: InventoryApiFactory.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Promotions.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ecommerce.Promotions.IntegrationTests;

/// <summary>API real contra un PostgreSQL real en un contenedor. Sin RabbitMQ: Promotions no lo usa.</summary>
public class PromotionsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-cupones-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("promotions_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__PromotionsDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        foreach (var name in new[] { "ConnectionStrings__PromotionsDb", "Jwt__Secret", "Jwt__Issuer", "Jwt__Audience" })
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        await _postgresContainer.DisposeAsync();
    }

    public static string CreateToken(Guid userId, string role = "Cliente")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Name, "Ana Pérez"),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer, audience: TestJwtAudience, claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

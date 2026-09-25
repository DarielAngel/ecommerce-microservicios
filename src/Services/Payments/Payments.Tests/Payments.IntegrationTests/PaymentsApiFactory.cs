using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ecommerce.Payments.IntegrationTests;

public class PaymentsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-pagos-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";

    public FakePayPalClient FakePayPal { get; } = new();

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("payments_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPayPalClient>();
            services.AddSingleton<IPayPalClient>(FakePayPal);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__PaymentsDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PaymentsDb", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);

        await _postgresContainer.DisposeAsync();
    }

    public static string CreateToken(Guid userId, string role = "Cliente")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer, audience: TestJwtAudience, claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

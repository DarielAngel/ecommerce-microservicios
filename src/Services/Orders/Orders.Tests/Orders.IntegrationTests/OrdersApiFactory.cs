using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ecommerce.Orders.IntegrationTests;

public class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-ordenes-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";
    private const string RabbitMqTestUsername = "test_user";
    private const string RabbitMqTestPassword = "test_pass";

    public FakeCartServiceClient FakeCart { get; } = new();
    public FakeInventoryServiceClient FakeInventory { get; } = new();
    public FakePaymentServiceClient FakePayments { get; } = new();
    public FakeCouponServiceClient FakeCoupons { get; } = new();

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // IMPORTANTE: NO usar "guest" aquí (restricción de loopback de RabbitMQ — ver Inventory).
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername(RabbitMqTestUsername)
        .WithPassword(RabbitMqTestPassword)
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICartServiceClient>();
            services.AddSingleton<ICartServiceClient>(FakeCart);

            services.RemoveAll<IInventoryServiceClient>();
            services.AddSingleton<IInventoryServiceClient>(FakeInventory);

            services.RemoveAll<IPaymentServiceClient>();
            services.AddSingleton<IPaymentServiceClient>(FakePayments);

            services.RemoveAll<ICouponServiceClient>();
            services.AddSingleton<ICouponServiceClient>(FakeCoupons);
        });
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgresContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitMqContainer.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", "/");
        Environment.SetEnvironmentVariable("RabbitMq__Username", RabbitMqTestUsername);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitMqTestPassword);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDb", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("RabbitMq__Host", null);
        Environment.SetEnvironmentVariable("RabbitMq__Port", null);
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", null);
        Environment.SetEnvironmentVariable("RabbitMq__Username", null);
        Environment.SetEnvironmentVariable("RabbitMq__Password", null);

        await Task.WhenAll(_postgresContainer.DisposeAsync().AsTask(), _rabbitMqContainer.DisposeAsync().AsTask());
    }

    /// <summary>
    /// Imita los tokens reales de Users, que incluyen email y nombre (Orders los lee del JWT
    /// para guardarlos en la orden). Con includeEmail=false se simula un token incompleto.
    /// </summary>
    public static string CreateToken(Guid userId, string role = "Cliente", bool includeEmail = true)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        if (includeEmail)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, $"{userId:N}@test.com"));
            claims.Add(new Claim(ClaimTypes.Name, "Cliente Prueba"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer, audience: TestJwtAudience, claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

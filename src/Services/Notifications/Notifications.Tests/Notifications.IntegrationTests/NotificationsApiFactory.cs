using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Infrastructure.Persistence;
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

namespace Ecommerce.Notifications.IntegrationTests;

/// <summary>Reemplaza a Resend en los tests: registra lo "enviado" en memoria (sin red ni API key).</summary>
public class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Html)> Sent { get; } = new();

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        Sent.Enqueue((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public class NotificationsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-notificaciones-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";
    private const string RabbitMqTestUsername = "test_user";
    private const string RabbitMqTestPassword = "test_pass";

    public FakeEmailSender FakeEmail { get; } = new();

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("notifications_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // IMPORTANTE: NO usar "guest" (restricción de loopback de RabbitMQ — ver Inventory).
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername(RabbitMqTestUsername)
        .WithPassword(RabbitMqTestPassword)
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(FakeEmail);
        });
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgresContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__NotificationsDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitMqContainer.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", "/");
        Environment.SetEnvironmentVariable("RabbitMq__Username", RabbitMqTestUsername);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitMqTestPassword);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        foreach (var name in new[]
        {
            "ConnectionStrings__NotificationsDb", "Jwt__Secret", "Jwt__Issuer", "Jwt__Audience",
            "RabbitMq__Host", "RabbitMq__Port", "RabbitMq__VirtualHost", "RabbitMq__Username", "RabbitMq__Password"
        })
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        await Task.WhenAll(_postgresContainer.DisposeAsync().AsTask(), _rabbitMqContainer.DisposeAsync().AsTask());
    }

    public static string CreateToken(string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
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

using Ecommerce.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ecommerce.Users.IntegrationTests;

/// <summary>
/// Levanta un contenedor real de Postgres (vía Testcontainers) y arranca la Api completa
/// en memoria contra él. Así probamos la integración real: Api + Application + Infrastructure + EF Core + Postgres,
/// no un mock. Requiere Docker corriendo en la máquina donde se ejecutan los tests.
/// </summary>
public class UsersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("users_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // IMPORTANTE: se usan variables de entorno en vez de ConfigureAppConfiguration/
        // AddInMemoryCollection. Con el modelo de hosting mínimo (WebApplication.CreateBuilder,
        // el que usa Program.cs) la sobreescritura vía ConfigureAppConfiguration NO tiene
        // garantizada prioridad sobre appsettings.json, y como appsettings.json trae
        // "UsersDb": "" (string vacío, no null), el chequeo "?? throw" no lo detecta y el
        // servicio termina intentando conectarse con una cadena vacía (error "Host can't be
        // null"). Las variables de entorno sí tienen prioridad garantizada y consistente.
        Environment.SetEnvironmentVariable("ConnectionStrings__UsersDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "clave-secreta-solo-para-tests-1234567890-abcdefgh");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Ecommerce.Users.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Ecommerce.Clients.Tests");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "15");
        Environment.SetEnvironmentVariable("AdminProvisioning__ApiKey", "test-admin-key");

        // Aplica el esquema (CreateAsync sirve para tests; en producción se usan migraciones reales).
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__UsersDb", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", null);
        Environment.SetEnvironmentVariable("AdminProvisioning__ApiKey", null);

        await _postgresContainer.DisposeAsync();
    }
}

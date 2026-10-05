using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ecommerce.Catalog.IntegrationTests;

public class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Debe coincidir con el secreto que usa TestJwtFactory para firmar los tokens de prueba.
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-catalogo-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";

    private const string RabbitMqTestUsername = "test_user";
    private const string RabbitMqTestPassword = "test_pass";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("catalog_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // Crear un producto publica VariantCreatedEvent (Inventario crea su stock a partir de él). Sin un broker
    // propio, la publicación reintenta contra el host "rabbitmq" —que no existe fuera de Docker Compose— hasta
    // que el HttpClient del test se rinde (100 s). Los tests no deben depender de infraestructura ajena.
    // IMPORTANTE: NO usar "guest" (restricción de loopback de RabbitMQ — ver Inventory).
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername(RabbitMqTestUsername)
        .WithPassword(RabbitMqTestPassword)
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgresContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        // Variables de entorno en vez de ConfigureAppConfiguration: ver el comentario detallado
        // en UsersApiFactory.cs (mismo bug, misma causa: appsettings.json trae "" en vez de null
        // y ConfigureAppConfiguration no tiene prioridad garantizada sobre él en hosting mínimo).
        Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("FileStorage__RootPath", Path.Combine(Path.GetTempPath(), "catalog-tests-uploads"));
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitMqContainer.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", "/");
        Environment.SetEnvironmentVariable("RabbitMq__Username", RabbitMqTestUsername);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitMqTestPassword);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("FileStorage__RootPath", null);
        foreach (var name in new[] { "RabbitMq__Host", "RabbitMq__Port", "RabbitMq__VirtualHost", "RabbitMq__Username", "RabbitMq__Password" })
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        await Task.WhenAll(_postgresContainer.DisposeAsync().AsTask(), _rabbitMqContainer.DisposeAsync().AsTask());
    }
}

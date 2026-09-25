using Ecommerce.Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ecommerce.Inventory.IntegrationTests;

public class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-inventario-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";

    // RabbitMqContainer no expone las credenciales configuradas como propiedades públicas
    // (a diferencia de PostgreSqlContainer.GetConnectionString()), así que las guardamos
    // nosotros mismos para usarlas tanto al construir el contenedor como al setear las
    // variables de entorno.
    private const string RabbitMqTestUsername = "test_user";
    private const string RabbitMqTestPassword = "test_pass";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("inventory_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        // IMPORTANTE: NO usar "guest" aquí. RabbitMQ solo permite loguearse como "guest" desde
        // conexiones que el broker vea como loopback real; como Testcontainers conecta a través
        // del mapeo de puertos de Docker, la conexión no cuenta como loopback y "guest" recibe
        // ACCESS_REFUSED aunque la contraseña sea correcta. Un usuario propio no tiene esa
        // restricción — por eso el test que sí llegaba a publicar/consumir vía RabbitMQ fallaba.
        .WithUsername(RabbitMqTestUsername)
        .WithPassword(RabbitMqTestPassword)
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgresContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        // Mismo patrón que UsersApiFactory/CatalogApiFactory: variables de entorno en vez de
        // ConfigureAppConfiguration, porque con hosting mínimo no tienen prioridad garantizada
        // sobre appsettings.json.
        Environment.SetEnvironmentVariable("ConnectionStrings__InventoryDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitMqContainer.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", "/");
        Environment.SetEnvironmentVariable("RabbitMq__Username", RabbitMqTestUsername);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitMqTestPassword);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__InventoryDb", null);
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
}

using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ecommerce.Cart.IntegrationTests;

public class CartApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "clave-secreta-solo-para-tests-carrito-1234567890";
    public const string TestJwtIssuer = "Ecommerce.Users.Tests";
    public const string TestJwtAudience = "Ecommerce.Clients.Tests";

    public FakeCatalogServiceClient FakeCatalog { get; } = new();
    public FakeInventoryServiceClient FakeInventory { get; } = new();

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("cart_db_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // A diferencia de la connection string (que usa variables de entorno por el problema de
        // precedencia ya conocido), reemplazar SERVICIOS sí es confiable con ConfigureTestServices
        // — es un mecanismo distinto, no de configuración sino de contenedor de DI.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICatalogServiceClient>();
            services.AddSingleton<ICatalogServiceClient>(FakeCatalog);

            services.RemoveAll<IInventoryServiceClient>();
            services.AddSingleton<IInventoryServiceClient>(FakeInventory);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__CartDb", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CartDb", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);

        await _postgresContainer.DisposeAsync();
    }
}

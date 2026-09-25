using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Cart.Infrastructure.Repositories;
using Ecommerce.Cart.Infrastructure.ServiceClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CartDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'CartDb' en la configuración.");
        }

        services.AddDbContext<CartDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICartRepository, CartRepository>();

        var serviceUrls = configuration.GetSection(ServiceUrlsOptions.SectionName).Get<ServiceUrlsOptions>()
            ?? new ServiceUrlsOptions();

        services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.CatalogBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<IInventoryServiceClient, InventoryServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.InventoryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}

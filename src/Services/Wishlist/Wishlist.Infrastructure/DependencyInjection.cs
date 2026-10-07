using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Infrastructure.Persistence;
using Ecommerce.Wishlist.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Wishlist.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WishlistDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'WishlistDb' en la configuración.");
        }

        services.AddDbContext<WishlistDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IWishlistRepository, WishlistRepository>();

        // Sin RabbitMQ: los favoritos no publican ni consumen eventos. Si un producto deja de existir,
        // la página "Mis favoritos" lo muestra como "ya no disponible" y el cliente puede quitarlo.
        return services;
    }
}

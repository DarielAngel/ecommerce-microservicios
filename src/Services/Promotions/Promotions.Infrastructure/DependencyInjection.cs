using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Infrastructure.Persistence;
using Ecommerce.Promotions.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Promotions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PromotionsDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'PromotionsDb' en la configuración.");
        }

        services.AddDbContext<PromotionsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IRedemptionRepository, RedemptionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Sin RabbitMQ: Órdenes llama a este servicio por HTTP durante la saga, igual que a Inventario.
        return services;
    }
}

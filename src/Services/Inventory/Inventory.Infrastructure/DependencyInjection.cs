using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Infrastructure.Messaging;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Ecommerce.Inventory.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'InventoryDb' en la configuración.");
        }

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.AddConsumer<VariantCreatedConsumer>();
            busConfigurator.AddConsumer<ReturnRestockConsumer>();

            busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
            {
                rabbitConfigurator.Host(rabbitMqSettings.Host, (ushort)rabbitMqSettings.Port, rabbitMqSettings.VirtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(rabbitMqSettings.Username);
                    hostConfigurator.Password(rabbitMqSettings.Password);
                });

                // Reintentos ante fallos transitorios (ej. Postgres momentáneamente no disponible)
                // antes de mandar el mensaje a la cola de errores.
                rabbitConfigurator.UseMessageRetry(retryConfigurator =>
                    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5)));

                rabbitConfigurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

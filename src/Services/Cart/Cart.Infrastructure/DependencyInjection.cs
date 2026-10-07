using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Infrastructure.Jobs;
using Ecommerce.Cart.Infrastructure.Messaging;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Cart.Infrastructure.Repositories;
using Ecommerce.Cart.Infrastructure.ServiceClients;
using MassTransit;
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

        // Recordatorio de carrito abandonado (Fase 6): un proceso en segundo plano + publicar por RabbitMQ.
        services.Configure<AbandonedCartOptions>(configuration.GetSection(AbandonedCartOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        var abandonedCart = configuration.GetSection(AbandonedCartOptions.SectionName).Get<AbandonedCartOptions>()
            ?? new AbandonedCartOptions();

        if (!abandonedCart.Enabled)
        {
            services.AddScoped<IEventPublisher, NoOpEventPublisher>();
            return services;
        }

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
            {
                rabbitConfigurator.Host(rabbitMqSettings.Host, (ushort)rabbitMqSettings.Port, rabbitMqSettings.VirtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(rabbitMqSettings.Username);
                    hostConfigurator.Password(rabbitMqSettings.Password);
                });
                rabbitConfigurator.ConfigureEndpoints(context);
            });
        });
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddHostedService<AbandonedCartWorker>();

        return services;
    }
}

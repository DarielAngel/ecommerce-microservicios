using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Infrastructure.Messaging;
using Ecommerce.Orders.Infrastructure.Persistence;
using Ecommerce.Orders.Infrastructure.Repositories;
using Ecommerce.Orders.Infrastructure.ServiceClients;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'OrdersDb' en la configuración.");
        }

        services.AddDbContext<OrdersDbContext>(options =>
            // Una orden trae sus líneas y sus devoluciones (con sus líneas) en una sola consulta: son pocas filas
            // por orden, y así una lectura nunca ve "media orden" entre dos consultas separadas.
            options.UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        // Órdenes solo PUBLICA eventos (OrderPaid, OrderShipped) — no consume nada, así que no
        // hace falta registrar consumidores, solo la conexión al bus.
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

        var serviceUrls = configuration.GetSection(ServiceUrlsOptions.SectionName).Get<ServiceUrlsOptions>()
            ?? new ServiceUrlsOptions();

        services.AddHttpClient<ICartServiceClient, CartServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.CartBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IInventoryServiceClient, InventoryServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.InventoryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.PaymentsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20); // Pagos habla con PayPal, puede tardar un poco más
        });

        services.AddHttpClient<ICouponServiceClient, CouponServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.PromotionsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IOrderLock, OrderLock>();

        services.AddHttpClient<ILoyaltyServiceClient, LoyaltyServiceClient>(client =>
        {
            client.BaseAddress = new Uri(serviceUrls.LoyaltyBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}

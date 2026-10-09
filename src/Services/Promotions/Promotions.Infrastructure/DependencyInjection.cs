using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Infrastructure.Messaging;
using Ecommerce.Promotions.Infrastructure.Persistence;
using Ecommerce.Promotions.Infrastructure.Repositories;
using MassTransit;
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

        // El checkout (reservar / confirmar / liberar) sigue siendo HTTP desde Órdenes. RabbitMQ solo trae el
        // OrderRefundedEvent (Fase 7): un pedido reembolsado completo devuelve el uso del cupón.
        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetEndpointNameFormatter(MessagingConventions.EndpointNameFormatter);
            busConfigurator.AddConsumer<OrderRefundedConsumer>();

            busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
            {
                rabbitConfigurator.Host(rabbitMqSettings.Host, (ushort)rabbitMqSettings.Port, rabbitMqSettings.VirtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(rabbitMqSettings.Username);
                    hostConfigurator.Password(rabbitMqSettings.Password);
                });

                rabbitConfigurator.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));

                rabbitConfigurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

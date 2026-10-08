using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Infrastructure.Messaging;
using Ecommerce.Loyalty.Infrastructure.Persistence;
using Ecommerce.Loyalty.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Loyalty.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LoyaltyDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'LoyaltyDb' en la configuración.");
        }

        services.AddDbContext<LoyaltyDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetEndpointNameFormatter(MessagingConventions.EndpointNameFormatter);
            busConfigurator.AddConsumer<OrderPaidConsumer>();
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

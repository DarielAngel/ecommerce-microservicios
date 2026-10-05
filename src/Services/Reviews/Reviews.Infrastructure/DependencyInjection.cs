using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Infrastructure.Messaging;
using Ecommerce.Reviews.Infrastructure.Persistence;
using Ecommerce.Reviews.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReviewsDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'ReviewsDb' en la configuración.");
        }

        services.AddDbContext<ReviewsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IVerifiedPurchaseRepository, VerifiedPurchaseRepository>();

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetEndpointNameFormatter(MessagingConventions.EndpointNameFormatter);
            busConfigurator.AddConsumer<OrderPaidConsumer>();

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

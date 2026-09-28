using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Infrastructure.Email;
using Ecommerce.Notifications.Infrastructure.Messaging;
using Ecommerce.Notifications.Infrastructure.Persistence;
using Ecommerce.Notifications.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationsDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'NotificationsDb' en la configuración.");
        }

        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();

        services.Configure<ResendSettings>(configuration.GetSection(ResendSettings.SectionName));
        var resendBaseUrl = (configuration[$"{ResendSettings.SectionName}:BaseUrl"] ?? "https://api.resend.com/").TrimEnd('/') + "/";

        services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri(resendBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.AddConsumer<UserRegisteredConsumer>();
            busConfigurator.AddConsumer<OrderPaidConsumer>();
            busConfigurator.AddConsumer<OrderShippedConsumer>();

            busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
            {
                rabbitConfigurator.Host(rabbitMqSettings.Host, (ushort)rabbitMqSettings.Port, rabbitMqSettings.VirtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(rabbitMqSettings.Username);
                    hostConfigurator.Password(rabbitMqSettings.Password);
                });

                // Si el proveedor de email falla (red, rate limit), reintenta antes de mandar el
                // mensaje a la cola de errores: el cliente igual termina recibiendo su email.
                rabbitConfigurator.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(10)));

                rabbitConfigurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

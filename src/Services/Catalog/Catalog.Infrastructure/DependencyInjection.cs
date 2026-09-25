using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Infrastructure.Messaging;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.Infrastructure.Repositories;
using Ecommerce.Catalog.Infrastructure.Storage;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CatalogDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'CatalogDb' en la configuración.");
        }

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                // Product tiene dos colecciones hijas (Variants e Images). Cargarlas juntas en
                // una sola consulta con dos LEFT JOIN produce un "cartesian explosion" que puede
                // confundir al change tracker (síntoma real visto: intentaba UPDATE una imagen
                // nueva en vez de INSERT). Split queries evita esto ejecutando una consulta por
                // colección; es la práctica recomendada por EF Core para este tipo de agregado.
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        // Catálogo solo PUBLICA eventos (no consume nada todavía), así que no se registran
        // consumidores aquí — solo la conexión al bus.
        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.UsingRabbitMq((context, rabbitConfigurator) =>
            {
                rabbitConfigurator.Host(rabbitMqSettings.Host, (ushort)rabbitMqSettings.Port, rabbitMqSettings.VirtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(rabbitMqSettings.Username);
                    hostConfigurator.Password(rabbitMqSettings.Password);
                });
            });
        });

        return services;
    }
}

using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Infrastructure.PayPal;
using Ecommerce.Payments.Infrastructure.Persistence;
using Ecommerce.Payments.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentsDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta la connection string 'PaymentsDb' en la configuración.");
        }

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IPaymentRepository, PaymentRepository>();

        services.Configure<PayPalSettings>(configuration.GetSection(PayPalSettings.SectionName));

        // Por defecto, siempre el cliente REAL de PayPal. El simulado solo se activa si se
        // pide explícitamente (PayPal:Provider=Fake) — pensado para desarrollo local, nunca
        // para producción. Es la misma interfaz IPayPalClient que ya usan los tests, ahora
        // también disponible para correr dentro de Docker.
        var useFakeProvider = string.Equals(
            configuration[$"{PayPalSettings.SectionName}:Provider"], "Fake", StringComparison.OrdinalIgnoreCase);

        if (useFakeProvider)
        {
            services.AddSingleton<IPayPalClient, FakePayPalClient>();
        }
        else
        {
            var payPalBaseUrl = configuration[$"{PayPalSettings.SectionName}:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";

            // Named client (no typed client) para el token provider: así PayPalAccessTokenProvider
            // puede registrarse como singleton "de verdad" (para que el cacheo del token en memoria
            // persista entre requests) sin chocar con el ciclo de vida que impone AddHttpClient<T>.
            services.AddHttpClient("PayPal-OAuth", client =>
            {
                client.BaseAddress = new Uri(payPalBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            });
            services.AddSingleton<PayPalAccessTokenProvider>();

            // PayPalClient sí puede ser un typed client normal (transient/scoped): no cachea nada
            // en memoria por sí mismo, delega el cacheo del token al provider de arriba.
            services.AddHttpClient<IPayPalClient, PayPalClient>(client =>
            {
                client.BaseAddress = new Uri(payPalBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            });
        }

        return services;
    }
}

using System.Reflection;
using Ecommerce.Promotions.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Promotions.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Reloj inyectable: las pruebas fijan "ahora" para probar vigencias y reservas vencidas.
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}

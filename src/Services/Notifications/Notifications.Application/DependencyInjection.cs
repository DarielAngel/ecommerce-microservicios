using System.Reflection;
using Ecommerce.Notifications.Application.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddScoped<NotificationDispatcher>();

        return services;
    }
}

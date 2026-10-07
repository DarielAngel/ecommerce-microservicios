using MassTransit;

namespace Ecommerce.Loyalty.Infrastructure.Messaging;

public static class MessagingConventions
{
    /// <summary>
    /// Prefijo "loyalty-" en el nombre de cada cola: así este servicio tiene SU cola para OrderPaid y
    /// recibe todos los eventos (si compartiera la de Notificaciones o Reseñas, RabbitMQ repartiría los
    /// eventos entre ellos en vez de dárselos a todos).
    /// </summary>
    public static IEndpointNameFormatter EndpointNameFormatter { get; } =
        new KebabCaseEndpointNameFormatter("loyalty", includeNamespace: false);
}

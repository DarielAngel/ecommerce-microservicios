using MassTransit;

namespace Ecommerce.Promotions.Infrastructure.Messaging;

public static class MessagingConventions
{
    /// <summary>
    /// Prefijo "promotions-" en el nombre de cada cola: así este servicio tiene SU cola para OrderRefunded y recibe
    /// todos los eventos (si compartiera la de Notificaciones, Inventario o Lealtad, RabbitMQ repartiría los eventos
    /// entre ellos en vez de dárselos a todos).
    /// </summary>
    public static IEndpointNameFormatter EndpointNameFormatter { get; } =
        new KebabCaseEndpointNameFormatter("promotions", includeNamespace: false);
}

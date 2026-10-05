using MassTransit;

namespace Ecommerce.Reviews.Infrastructure.Messaging;

public static class MessagingConventions
{
    /// <summary>
    /// Prefijo "reviews-" en el nombre de cada cola. SIN esto, el consumidor de este servicio se llamaría
    /// igual que el de Notificaciones ("OrderPaid") y ambos compartirían UNA cola: RabbitMQ le entregaría
    /// cada evento solo a uno de los dos (reparto de trabajo) en vez de a los dos (suscripción).
    /// Con una cola propia por servicio, cada evento llega a todos los que lo necesitan.
    /// </summary>
    public static IEndpointNameFormatter EndpointNameFormatter { get; } =
        new KebabCaseEndpointNameFormatter("reviews", includeNamespace: false);
}

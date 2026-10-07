using Ecommerce.Cart.Application.Common;
using MassTransit;

namespace Ecommerce.Cart.Infrastructure.Messaging;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

/// <summary>Carrito solo PUBLICA (el recordatorio de carrito abandonado); no consume nada.</summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class =>
        _publishEndpoint.Publish(integrationEvent, ct);
}

/// <summary>Con el recordatorio apagado no hay bus: lo que se publique se descarta.</summary>
public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class => Task.CompletedTask;
}

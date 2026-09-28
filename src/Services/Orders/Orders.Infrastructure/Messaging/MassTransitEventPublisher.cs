using Ecommerce.Orders.Application.Common;
using MassTransit;

namespace Ecommerce.Orders.Infrastructure.Messaging;

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class =>
        _publishEndpoint.Publish(integrationEvent, ct);
}

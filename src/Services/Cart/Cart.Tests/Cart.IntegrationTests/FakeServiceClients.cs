using System.Collections.Concurrent;
using Ecommerce.Cart.Application.Common;

namespace Ecommerce.Cart.IntegrationTests;

/// <summary>
/// Reemplaza al CatalogServiceClient real en los tests. Configúralo desde el test con
/// Variants[variantId] = new VariantInfo(...) antes de llamar al endpoint.
/// </summary>
public class FakeCatalogServiceClient : ICatalogServiceClient
{
    public ConcurrentDictionary<Guid, VariantInfo> Variants { get; } = new();

    public Task<VariantInfo?> GetVariantAsync(Guid variantId, CancellationToken ct) =>
        Task.FromResult(Variants.TryGetValue(variantId, out var variant) ? variant : null);
}

/// <summary>
/// Reemplaza al InventoryServiceClient real. Configúralo desde el test con
/// AvailableQuantities[variantId] = 10 antes de llamar al endpoint.
/// </summary>
public class FakeInventoryServiceClient : IInventoryServiceClient
{
    public ConcurrentDictionary<Guid, int> AvailableQuantities { get; } = new();

    public Task<int> GetAvailableQuantityAsync(Guid variantId, string accessToken, CancellationToken ct) =>
        Task.FromResult(AvailableQuantities.TryGetValue(variantId, out var quantity) ? quantity : 0);
}

public class CapturingEventPublisher : IEventPublisher
{
    public System.Collections.Concurrent.ConcurrentQueue<object> Events { get; } = new();

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class
    {
        Events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}

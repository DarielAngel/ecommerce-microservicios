using Ecommerce.Orders.Domain.Entities;

namespace Ecommerce.Orders.Application.Common;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct);
    Task<IReadOnlyList<Order>> ListByUserIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Order>> ListAllAsync(int count, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// ---- Carrito ----

public record CartItemInfo(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity);

public interface ICartServiceClient
{
    Task<IReadOnlyList<CartItemInfo>> GetCartItemsAsync(string accessToken, CancellationToken ct);
    Task RemoveItemAsync(Guid variantId, string accessToken, CancellationToken ct);
}

// ---- Inventario ----

public record ReservationLineInput(Guid VariantId, int Quantity);

public interface IInventoryServiceClient
{
    /// <summary>Todo o nada: si algún ítem no tiene stock suficiente, no se reserva nada.</summary>
    Task<bool> ReserveStockAsync(Guid orderId, IReadOnlyList<ReservationLineInput> items, string accessToken, CancellationToken ct);
    Task ConfirmReservationAsync(Guid orderId, string accessToken, CancellationToken ct);
    Task ReleaseReservationAsync(Guid orderId, string accessToken, CancellationToken ct);
}

// ---- Pagos ----

public record CreatePaymentResult(string Status, string? ApproveUrl);
public record CapturePaymentResult(bool Success, string Status);

public interface IPaymentServiceClient
{
    Task<CreatePaymentResult> CreatePaymentAsync(Guid orderId, decimal amount, string currency, string accessToken, CancellationToken ct);
    Task<CapturePaymentResult> CapturePaymentAsync(Guid orderId, string accessToken, CancellationToken ct);
}

// ---- Eventos ----

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class;
}

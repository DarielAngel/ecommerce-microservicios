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

/// <summary>
/// Bloqueo de UNA orden mientras se confirma su pago: dos "confirmar" a la vez (doble clic, dos
/// pestañas) se atienden de a uno, y el segundo ya ve la orden pagada. Se libera al hacer DisposeAsync.
/// </summary>
public interface IOrderLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct);
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

// ---- Promociones (cupones) ----

public record CouponReservation(string Code, decimal DiscountAmount);

/// <summary>
/// Mismo ciclo que el stock: reservar durante el checkout, confirmar si el pago se captura, liberar si
/// falla. Si Promociones rechaza el cupón (vencido, agotado, compra mínima...), ReserveAsync lanza
/// ConflictAppException con el mensaje de Promociones, listo para mostrárselo al cliente.
/// </summary>
public interface ICouponServiceClient
{
    Task<CouponReservation> ReserveAsync(Guid orderId, string code, decimal subtotal, string accessToken, CancellationToken ct);
    Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct);
    Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct);
}

// ---- Puntos (Lealtad, Fase 6) ----

public record LoyaltyReservation(int Points, decimal DiscountAmount);

/// <summary>
/// Mismo ciclo que el cupón. Lealtad decide cuántos puntos usar (todos los que entren sin pasar la mitad
/// de <c>amount</c>) y el descuento; si el cliente no tiene suficientes, ReserveAsync lanza
/// ConflictAppException con el motivo, listo para mostrárselo.
/// </summary>
public interface ILoyaltyServiceClient
{
    Task<LoyaltyReservation> ReserveAsync(Guid orderId, decimal amount, string accessToken, CancellationToken ct);
    Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct);
    Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct);
}

// ---- Eventos ----

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class;
}

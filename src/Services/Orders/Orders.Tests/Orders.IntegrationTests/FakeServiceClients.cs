using System.Collections.Concurrent;
using Ecommerce.Orders.Application.Common;

namespace Ecommerce.Orders.IntegrationTests;

public class FakeCartServiceClient : ICartServiceClient
{
    public List<CartItemInfo> Items { get; set; } = new();
    public List<Guid> RemovedVariantIds { get; } = new();

    public Task<IReadOnlyList<CartItemInfo>> GetCartItemsAsync(string accessToken, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CartItemInfo>>(Items);

    public Task RemoveItemAsync(Guid variantId, string accessToken, CancellationToken ct)
    {
        RemovedVariantIds.Add(variantId);
        return Task.CompletedTask;
    }
}

public class FakeInventoryServiceClient : IInventoryServiceClient
{
    public bool ShouldReserveSucceed { get; set; } = true;
    public ConcurrentDictionary<Guid, bool> ConfirmedOrders { get; } = new();
    public ConcurrentDictionary<Guid, bool> ReleasedOrders { get; } = new();

    public Task<bool> ReserveStockAsync(Guid orderId, IReadOnlyList<ReservationLineInput> items, string accessToken, CancellationToken ct) =>
        Task.FromResult(ShouldReserveSucceed);

    public Task ConfirmReservationAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ConfirmedOrders[orderId] = true;
        return Task.CompletedTask;
    }

    public Task ReleaseReservationAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ReleasedOrders[orderId] = true;
        return Task.CompletedTask;
    }
}

public class FakePaymentServiceClient : IPaymentServiceClient
{
    public bool ShouldCaptureSucceed { get; set; } = true;

    public Task<CreatePaymentResult> CreatePaymentAsync(Guid orderId, decimal amount, string currency, string accessToken, CancellationToken ct) =>
        Task.FromResult(new CreatePaymentResult("PendingApproval", $"https://fake-paypal.test/approve/{orderId}"));

    public Task<CapturePaymentResult> CapturePaymentAsync(Guid orderId, string accessToken, CancellationToken ct) =>
        Task.FromResult(new CapturePaymentResult(ShouldCaptureSucceed, ShouldCaptureSucceed ? "Captured" : "Failed"));

    /// <summary>Reembolsos pedidos (por id de devolución). Con <see cref="RefundFailure"/> se simula que PayPal dice que no.</summary>
    public ConcurrentDictionary<Guid, decimal> Refunds { get; } = new();
    public string? RefundFailure { get; set; }

    public Task<RefundPaymentResult> RefundAsync(Guid orderId, Guid refundId, decimal amount, string? reason, string accessToken, CancellationToken ct)
    {
        if (RefundFailure is not null) throw new ConflictAppException(RefundFailure);
        Refunds.AddOrUpdate(refundId, amount, (_, previous) => previous);
        return Task.FromResult(new RefundPaymentResult(refundId, amount, $"FAKE-REFUND-{refundId}", "Captured"));
    }

    public Task<RefundPaymentResult?> FindRefundAsync(Guid orderId, Guid refundId, string accessToken, CancellationToken ct) =>
        Task.FromResult(Refunds.TryGetValue(refundId, out var amount)
            ? new RefundPaymentResult(refundId, amount, $"FAKE-REFUND-{refundId}", "Captured")
            : null);
}

/// <summary>Guarda los eventos en memoria en vez de mandarlos a RabbitMQ, para poder revisarlos en las pruebas.</summary>
public class CapturingEventPublisher : IEventPublisher
{
    public ConcurrentQueue<object> Events { get; } = new();

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : class
    {
        Events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}

public class FakeCouponServiceClient : ICouponServiceClient
{
    /// <summary>Código aceptado → descuento. Cualquier otro código se rechaza como lo haría Promociones.</summary>
    public ConcurrentDictionary<string, decimal> ValidCoupons { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<Guid, decimal> ReservedSubtotals { get; } = new();
    public ConcurrentDictionary<Guid, bool> ConfirmedOrders { get; } = new();
    public ConcurrentDictionary<Guid, bool> ReleasedOrders { get; } = new();

    public Task<CouponReservation> ReserveAsync(Guid orderId, string code, decimal subtotal, string accessToken, CancellationToken ct)
    {
        if (!ValidCoupons.TryGetValue(code.Trim(), out var discount))
            throw new ConflictAppException($"El cupón \"{code.Trim().ToUpperInvariant()}\" no existe.");

        ReservedSubtotals[orderId] = subtotal;
        return Task.FromResult(new CouponReservation(code.Trim().ToUpperInvariant(), discount));
    }

    public Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ConfirmedOrders[orderId] = true;
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ReleasedOrders[orderId] = true;
        return Task.CompletedTask;
    }
}

public class FakeLoyaltyServiceClient : ILoyaltyServiceClient
{
    /// <summary>Puntos disponibles del "cliente" de las pruebas; la regla es la de Lealtad: hasta la mitad.</summary>
    public int Balance { get; set; }
    public ConcurrentDictionary<Guid, decimal> ReservedAmounts { get; } = new();
    public ConcurrentDictionary<Guid, bool> ConfirmedOrders { get; } = new();
    public ConcurrentDictionary<Guid, bool> ReleasedOrders { get; } = new();

    public Task<LoyaltyReservation> ReserveAsync(Guid orderId, decimal amount, string accessToken, CancellationToken ct)
    {
        var points = Math.Min(Balance, (int)Math.Floor(amount * 0.5m / 0.01m));
        if (points < 100) throw new ConflictAppException($"Necesitas al menos 100 puntos para usarlos (tienes {Balance}).");

        ReservedAmounts[orderId] = amount;
        return Task.FromResult(new LoyaltyReservation(points, points * 0.01m));
    }

    public Task ConfirmAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ConfirmedOrders[orderId] = true;
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(Guid orderId, string accessToken, CancellationToken ct)
    {
        ReleasedOrders[orderId] = true;
        return Task.CompletedTask;
    }
}

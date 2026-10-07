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

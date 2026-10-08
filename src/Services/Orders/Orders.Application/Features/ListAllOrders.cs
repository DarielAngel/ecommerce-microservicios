using Ecommerce.Orders.Application.Common;
using MediatR;

namespace Ecommerce.Orders.Application.Features;

public record ListAllOrdersQuery(int Count = 100) : IRequest<IReadOnlyList<AdminOrderResult>>;

public record AdminOrderResult(
    Guid OrderId, Guid UserId, string UserEmail, string UserFullName, string ShippingAddress,
    string Status, decimal TotalAmount, DateTime CreatedAtUtc, IReadOnlyList<OrderLineResult> Lines,
    decimal DiscountAmount, string? CouponCode, DateTime? PaidAtUtc = null, DateTime? ShippedAtUtc = null,
    int LoyaltyPoints = 0, decimal LoyaltyDiscount = 0,
    decimal RefundedAmount = 0, bool HasPendingCancellation = false, DateTime? CancelledAtUtc = null);

public class ListAllOrdersQueryHandler : IRequestHandler<ListAllOrdersQuery, IReadOnlyList<AdminOrderResult>>
{
    private readonly IOrderRepository _orderRepository;

    public ListAllOrdersQueryHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async Task<IReadOnlyList<AdminOrderResult>> Handle(ListAllOrdersQuery request, CancellationToken ct)
    {
        var count = Math.Clamp(request.Count, 1, 500);
        var orders = await _orderRepository.ListAllAsync(count, ct);

        return orders.Select(order => new AdminOrderResult(
            order.Id, order.UserId, order.UserEmail, order.UserFullName, order.ShippingAddress,
            order.Status.ToString(), order.TotalAmount, order.CreatedAtUtc,
            order.Lines.Select(OrderLineResult.From).ToList(),
            order.DiscountAmount, order.CouponCode, order.PaidAtUtc, order.ShippedAtUtc,
            order.LoyaltyPoints, order.LoyaltyDiscount,
            order.RefundedAmount, order.OpenReturn is { IsCancellation: true }, order.CancelledAtUtc
        )).ToList();
    }
}

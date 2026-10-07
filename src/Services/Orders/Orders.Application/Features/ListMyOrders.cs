using Ecommerce.Orders.Application.Common;
using MediatR;

namespace Ecommerce.Orders.Application.Features;

public record ListMyOrdersQuery(Guid UserId) : IRequest<IReadOnlyList<CheckoutResult>>;

public class ListMyOrdersQueryHandler : IRequestHandler<ListMyOrdersQuery, IReadOnlyList<CheckoutResult>>
{
    private readonly IOrderRepository _orderRepository;

    public ListMyOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<IReadOnlyList<CheckoutResult>> Handle(ListMyOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orderRepository.ListByUserIdAsync(request.UserId, ct);

        return orders.Select(order => CheckoutResult.From(order)).ToList();
    }
}

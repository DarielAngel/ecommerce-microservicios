using Ecommerce.Orders.Application.Common;
using MediatR;

namespace Ecommerce.Orders.Application.Features;

public record GetOrderByIdQuery(Guid OrderId, Guid RequestingUserId, bool RequestingUserIsAdmin) : IRequest<CheckoutResult>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, CheckoutResult>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<CheckoutResult> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("La orden no existe.");

        if (order.UserId != request.RequestingUserId && !request.RequestingUserIsAdmin)
        {
            // Un cliente no puede ver la orden de otro cliente. Devolvemos 404 en vez de 403
            // a propósito: así no confirmamos ni siquiera que el Id de la orden existe.
            throw new NotFoundAppException("La orden no existe.");
        }

        return new(
            order.Id, order.Status.ToString(), order.TotalAmount,
            order.Lines.Select(l => new OrderLineResult(l.VariantId, l.ProductName, l.Sku, l.UnitPrice, l.Quantity, l.LineTotal)).ToList(),
            ApproveUrl: null);
    }
}

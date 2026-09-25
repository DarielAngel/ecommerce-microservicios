using Ecommerce.Payments.Application.Common;
using MediatR;

namespace Ecommerce.Payments.Application.Features;

public record GetPaymentByOrderIdQuery(Guid OrderId) : IRequest<PaymentResult>;

public class GetPaymentByOrderIdQueryHandler : IRequestHandler<GetPaymentByOrderIdQuery, PaymentResult>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentByOrderIdQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<PaymentResult> Handle(GetPaymentByOrderIdQuery request, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("No hay ningún pago iniciado para esa orden.");

        return CreatePaymentCommandHandler.MapToResult(payment, approveUrl: null);
    }
}

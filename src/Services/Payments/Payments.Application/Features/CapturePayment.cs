using Ecommerce.Payments.Application.Common;
using MediatR;

namespace Ecommerce.Payments.Application.Features;

public record CapturePaymentCommand(Guid OrderId) : IRequest<PaymentResult>;

public class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentResult>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayPalClient _payPalClient;

    public CapturePaymentCommandHandler(IPaymentRepository paymentRepository, IPayPalClient payPalClient)
    {
        _paymentRepository = paymentRepository;
        _payPalClient = payPalClient;
    }

    public async Task<PaymentResult> Handle(CapturePaymentCommand request, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("No hay ningún pago iniciado para esa orden.");

        // Idempotente: si ya está capturado, devolvemos el resultado actual sin volver a
        // llamar a PayPal (evita una captura duplicada si el caller reintenta).
        if (payment.Status == Domain.Enums.PaymentStatus.Captured)
        {
            return CreatePaymentCommandHandler.MapToResult(payment, approveUrl: null);
        }

        var captureResult = await _payPalClient.CaptureOrderAsync(payment.PayPalOrderId, ct);

        if (!captureResult.Success)
        {
            payment.MarkFailed(captureResult.FailureReason ?? "PayPal rechazó la captura del pago.");
            await _paymentRepository.SaveChangesAsync(ct);
            throw new ConflictAppException(payment.FailureReason!);
        }

        payment.MarkCaptured(captureResult.CaptureId!);
        await _paymentRepository.SaveChangesAsync(ct);

        return CreatePaymentCommandHandler.MapToResult(payment, approveUrl: null);
    }
}

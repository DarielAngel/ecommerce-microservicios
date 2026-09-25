using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Payments.Application.Features;

public record CreatePaymentCommand(Guid OrderId, decimal Amount, string Currency) : IRequest<PaymentResult>;

public record PaymentResult(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Status,
    string? ApproveUrl, string? PayPalCaptureId, DateTime? CapturedAtUtc);

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentResult>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayPalClient _payPalClient;

    public CreatePaymentCommandHandler(IPaymentRepository paymentRepository, IPayPalClient payPalClient)
    {
        _paymentRepository = paymentRepository;
        _payPalClient = payPalClient;
    }

    public async Task<PaymentResult> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        // Idempotencia: si ya hay un pago pendiente o capturado para esta orden, no creamos
        // uno nuevo — devolvemos el que ya existe. Esto protege contra doble-click del usuario
        // o un reintento del caller (Órdenes, más adelante) tras un timeout de red.
        var existingPayment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, ct);

        if (existingPayment is not null)
        {
            return MapToResult(existingPayment, approveUrl: null);
        }

        var paypalOrder = await _payPalClient.CreateOrderAsync(request.Amount, request.Currency, request.OrderId, ct);

        var payment = Payment.Create(request.OrderId, request.Amount, request.Currency, paypalOrder.PayPalOrderId);

        await _paymentRepository.AddAsync(payment, ct);
        await _paymentRepository.SaveChangesAsync(ct);

        return MapToResult(payment, paypalOrder.ApproveUrl);
    }

    internal static PaymentResult MapToResult(Payment payment, string? approveUrl) => new(
        payment.Id, payment.OrderId, payment.Amount, payment.Currency, payment.Status.ToString(),
        approveUrl, payment.PayPalCaptureId, payment.CapturedAtUtc);
}

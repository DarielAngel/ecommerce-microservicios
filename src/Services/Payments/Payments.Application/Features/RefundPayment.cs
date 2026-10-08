using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payments.Application.Features;

/// <summary>
/// Devuelve (todo o parte de) lo cobrado en una orden (Fase 7). <paramref name="RefundId"/> lo elige quien lo
/// pide (Órdenes usa el id de la devolución) y es la clave de idempotencia: pedir dos veces el mismo
/// reembolso devuelve el que ya existe, nunca paga dos veces.
/// </summary>
public record RefundPaymentCommand(Guid OrderId, Guid RefundId, decimal Amount, string? Reason) : IRequest<RefundResult>;

public record RefundResult(
    Guid RefundId, Guid OrderId, decimal Amount, string PayPalRefundId, decimal TotalRefunded, decimal Refundable,
    string PaymentStatus, DateTime CreatedAtUtc);

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto a reembolsar debe ser mayor a cero.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("El monto a reembolsar admite como máximo 2 decimales.");
        RuleFor(x => x.Reason).MaximumLength(255);
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, RefundResult>
{
    private readonly IPaymentRepository _payments;
    private readonly IPayPalClient _payPal;
    private readonly TimeProvider _time;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IPaymentRepository payments, IPayPalClient payPal, TimeProvider time, ILogger<RefundPaymentCommandHandler> logger)
    {
        _payments = payments;
        _payPal = payPal;
        _time = time;
        _logger = logger;
    }

    public async Task<RefundResult> Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        var payment = await _payments.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("No hay ningún pago para esa orden.");

        // Reintento del mismo reembolso (timeout, doble clic del Admin): se devuelve el que ya existe.
        if (payment.FindRefund(request.RefundId) is { } existing)
        {
            if (existing.Amount != request.Amount)
                throw new ConflictAppException(
                    $"Ese reembolso ya se hizo por {existing.Amount:F2} {payment.Currency}, no por {request.Amount:F2}.");
            return Map(payment, existing);
        }

        try
        {
            payment.EnsureCanRefund(request.Amount);
        }
        catch (DomainException ex)
        {
            throw new ConflictAppException(ex.Message);
        }

        var result = await _payPal.RefundCaptureAsync(
            payment.PayPalCaptureId!, request.Amount, payment.Currency, request.RefundId.ToString(), request.Reason, ct);
        if (!result.Success)
        {
            _logger.LogWarning("PayPal no reembolsó la orden {OrderId}: {Reason}", request.OrderId, result.FailureReason);
            throw new ConflictAppException(result.FailureReason ?? "PayPal rechazó el reembolso.");
        }

        var refund = payment.AddRefund(request.RefundId, request.Amount, result.RefundId!, request.Reason, _time.GetUtcNow().UtcDateTime);
        await _payments.SaveChangesAsync(ct);
        _logger.LogInformation("Reembolso {RefundId} de {Amount} {Currency} para la orden {OrderId} ({PayPalStatus}).",
            refund.Id, refund.Amount, payment.Currency, payment.OrderId, result.Status);

        return Map(payment, refund);
    }

    internal static RefundResult Map(Domain.Entities.Payment payment, Domain.Entities.PaymentRefund refund) => new(
        refund.Id, payment.OrderId, refund.Amount, refund.PayPalRefundId, payment.RefundedAmount, payment.RefundableAmount,
        payment.Status.ToString(), refund.CreatedAtUtc);
}

public record GetRefundQuery(Guid OrderId, Guid RefundId) : IRequest<RefundResult>;

public class GetRefundQueryHandler : IRequestHandler<GetRefundQuery, RefundResult>
{
    private readonly IPaymentRepository _payments;

    public GetRefundQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<RefundResult> Handle(GetRefundQuery request, CancellationToken ct)
    {
        var payment = await _payments.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException("No hay ningún pago para esa orden.");
        var refund = payment.FindRefund(request.RefundId)
            ?? throw new NotFoundAppException("Ese reembolso no existe.");
        return RefundPaymentCommandHandler.Map(payment, refund);
    }
}

using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record ReservationLineInput(Guid VariantId, int Quantity);

public record ReserveStockCommand(Guid OrderId, List<ReservationLineInput> Items) : IRequest<ReservationResult>;

public record ReservationLineResultDto(Guid VariantId, int Quantity);

public record ReservationResult(
    Guid ReservationId,
    Guid OrderId,
    string Status,
    IReadOnlyList<ReservationLineResultDto> Lines);

public class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("La reserva debe incluir al menos un ítem.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, ReservationResult>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReserveStockCommandHandler(
        IStockItemRepository stockItemRepository,
        IStockReservationRepository reservationRepository,
        IUnitOfWork unitOfWork)
    {
        _stockItemRepository = stockItemRepository;
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReservationResult> Handle(ReserveStockCommand request, CancellationToken ct)
    {
        // Idempotencia: si Órdenes reintenta la misma orden (timeout de red, reintento de saga),
        // no volvemos a reservar ni descontamos stock dos veces — devolvemos la reserva existente.
        var existing = await _reservationRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
        {
            return MapToResult(existing);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            // Todo o nada: si CUALQUIER línea no tiene stock suficiente, no se reserva ninguna.
            // TryReserveAsync es una actualización SQL atómica (UPDATE ... WHERE disponible >= cantidad),
            // así que es segura ante checkouts concurrentes sobre la misma variante.
            foreach (var item in request.Items)
            {
                var reserved = await _stockItemRepository.TryReserveAsync(item.VariantId, item.Quantity, innerCt);
                if (!reserved)
                {
                    throw new InsufficientStockAppException(
                        $"No hay suficiente stock disponible para la variante '{item.VariantId}' (se pidieron {item.Quantity}).");
                }
            }

            var reservation = StockReservation.Create(
                request.OrderId, request.Items.Select(i => (i.VariantId, i.Quantity)));

            await _reservationRepository.AddAsync(reservation, innerCt);
            await _reservationRepository.SaveChangesAsync(innerCt);

            return MapToResult(reservation);
        }, ct);
    }

    private static ReservationResult MapToResult(StockReservation reservation) => new(
        reservation.Id,
        reservation.OrderId,
        reservation.Status.ToString(),
        reservation.Lines.Select(l => new ReservationLineResultDto(l.VariantId, l.Quantity)).ToList());
}

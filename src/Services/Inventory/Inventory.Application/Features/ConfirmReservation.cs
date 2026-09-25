using Ecommerce.Inventory.Application.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record ConfirmReservationCommand(Guid OrderId) : IRequest<ReservationResult>;

public class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand, ReservationResult>
{
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmReservationCommandHandler(
        IStockReservationRepository reservationRepository,
        IStockItemRepository stockItemRepository,
        IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _stockItemRepository = stockItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReservationResult> Handle(ConfirmReservationCommand request, CancellationToken ct)
    {
        var reservation = await _reservationRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException($"No existe ninguna reserva de stock para la orden '{request.OrderId}'.");

        // Idempotente: si ya estaba confirmada (reintento de la saga), no volvemos a descontar.
        if (reservation.Status == Domain.Entities.ReservationStatus.Confirmed)
        {
            return MapToResult(reservation);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var line in reservation.Lines)
            {
                await _stockItemRepository.ConfirmReservedAsync(line.VariantId, line.Quantity, innerCt);
            }

            reservation.Confirm();
            await _reservationRepository.SaveChangesAsync(innerCt);

            return MapToResult(reservation);
        }, ct);
    }

    private static ReservationResult MapToResult(Domain.Entities.StockReservation reservation) => new(
        reservation.Id,
        reservation.OrderId,
        reservation.Status.ToString(),
        reservation.Lines.Select(l => new ReservationLineResultDto(l.VariantId, l.Quantity)).ToList());
}

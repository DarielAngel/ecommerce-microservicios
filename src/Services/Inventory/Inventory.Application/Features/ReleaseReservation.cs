using Ecommerce.Inventory.Application.Common;
using MediatR;

namespace Ecommerce.Inventory.Application.Features;

public record ReleaseReservationCommand(Guid OrderId) : IRequest<ReservationResult>;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, ReservationResult>
{
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReleaseReservationCommandHandler(
        IStockReservationRepository reservationRepository,
        IStockItemRepository stockItemRepository,
        IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _stockItemRepository = stockItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReservationResult> Handle(ReleaseReservationCommand request, CancellationToken ct)
    {
        var reservation = await _reservationRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundAppException($"No existe ninguna reserva de stock para la orden '{request.OrderId}'.");

        // Idempotente: si ya estaba liberada (reintento de la saga), no hacemos nada más.
        if (reservation.Status == Domain.Entities.ReservationStatus.Released)
        {
            return MapToResult(reservation);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var line in reservation.Lines)
            {
                await _stockItemRepository.ReleaseReservedAsync(line.VariantId, line.Quantity, innerCt);
            }

            reservation.Release();
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

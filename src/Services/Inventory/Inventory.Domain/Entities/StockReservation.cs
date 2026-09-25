using Ecommerce.Inventory.Domain.Exceptions;

namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// Representa la reserva de stock de UNA orden completa (puede tener varias variantes/líneas).
/// El flujo esperado, llamado por el orquestador de checkout de Órdenes (futuro Módulo 6):
/// 1. ReserveStock  -> crea esta reserva en estado Active y descuenta QuantityAvailable de cada StockItem.
/// 2a. Si el pago se confirma -> ConfirmReservation -> descuenta QuantityOnHand definitivamente.
/// 2b. Si el pago falla/se cancela -> ReleaseReservation -> libera QuantityReserved sin tocar QuantityOnHand.
/// </summary>
public class StockReservation
{
    private readonly List<StockReservationLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }

    public IReadOnlyCollection<StockReservationLine> Lines => _lines.AsReadOnly();

    private StockReservation() { }

    private StockReservation(Guid orderId)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Status = ReservationStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static StockReservation Create(Guid orderId, IEnumerable<(Guid VariantId, int Quantity)> items)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("OrderId es requerido para crear una reserva de stock.");
        }

        var reservation = new StockReservation(orderId);

        foreach (var (variantId, quantity) in items)
        {
            if (quantity <= 0)
            {
                throw new DomainException($"La cantidad para la variante {variantId} debe ser mayor a 0.");
            }

            reservation._lines.Add(new StockReservationLine(reservation.Id, variantId, quantity));
        }

        if (reservation._lines.Count == 0)
        {
            throw new DomainException("Una reserva de stock debe tener al menos una línea.");
        }

        return reservation;
    }

    public void Confirm()
    {
        EnsureActive();
        Status = ReservationStatus.Confirmed;
        ConfirmedAtUtc = DateTime.UtcNow;
    }

    public void Release()
    {
        EnsureActive();
        Status = ReservationStatus.Released;
        ReleasedAtUtc = DateTime.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException(
                $"La reserva ya está en estado '{Status}' y no puede volver a confirmarse/liberarse.");
        }
    }
}

using Ecommerce.Orders.Domain.Enums;

namespace Ecommerce.Orders.Domain.Entities;

/// <summary>
/// Ventana de entrega estimada que se le muestra al cliente (Fase 5, T5.2). Cuenta días hábiles
/// (lunes a viernes) desde el último hito del pedido:
/// <list type="bullet">
/// <item>pagado y todavía sin enviar: entre <see cref="PaidMinDays"/> y <see cref="PaidMaxDays"/> días hábiles desde el pago;</item>
/// <item>enviado: entre <see cref="ShippedMinDays"/> y <see cref="ShippedMaxDays"/> días hábiles desde el envío.</item>
/// </list>
/// Sin pago (pendiente, fallido, cancelado) no hay estimación.
/// </summary>
public readonly record struct DeliveryEstimate(DateOnly From, DateOnly To)
{
    public const int PaidMinDays = 3;
    public const int PaidMaxDays = 6;
    public const int ShippedMinDays = 1;
    public const int ShippedMaxDays = 3;

    public static DeliveryEstimate? For(OrderStatus status, DateTime? paidAtUtc, DateTime? shippedAtUtc) => status switch
    {
        OrderStatus.Paid when paidAtUtc is { } paid =>
            new DeliveryEstimate(AddBusinessDays(paid, PaidMinDays), AddBusinessDays(paid, PaidMaxDays)),
        OrderStatus.Shipped when (shippedAtUtc ?? paidAtUtc) is { } shipped =>
            new DeliveryEstimate(AddBusinessDays(shipped, ShippedMinDays), AddBusinessDays(shipped, ShippedMaxDays)),
        _ => null
    };

    public static DateOnly AddBusinessDays(DateTime fromUtc, int days)
    {
        var date = DateOnly.FromDateTime(fromUtc);
        var added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) added++;
        }
        return date;
    }
}

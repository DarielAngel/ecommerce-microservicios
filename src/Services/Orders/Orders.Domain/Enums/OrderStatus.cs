namespace Ecommerce.Orders.Domain.Enums;

public enum OrderStatus
{
    /// <summary>Stock reservado, pago creado en PayPal, esperando que el comprador apruebe y se capture.</summary>
    PendingPayment = 0,

    /// <summary>Pago capturado y stock confirmado (descontado definitivamente).</summary>
    Paid = 1,

    /// <summary>Algo falló (sin stock suficiente, o el pago fue rechazado): el stock reservado se liberó.</summary>
    Failed = 2,

    /// <summary>Cancelada explícitamente (uso futuro).</summary>
    Cancelled = 3,

    /// <summary>El Admin marcó la orden como enviada.</summary>
    Shipped = 4
}

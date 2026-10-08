namespace Ecommerce.Notifications.Domain.Enums;

public enum NotificationType
{
    UserRegistered = 0,
    OrderPaid = 1,
    OrderShipped = 2,

    /// <summary>Recordatorio de carrito abandonado (Fase 6). ReferenceId = id del recordatorio.</summary>
    CartAbandoned = 3,

    /// <summary>Devolución reembolsada (Fase 7). ReferenceId = id de la devolución.</summary>
    ReturnRefunded = 4,

    /// <summary>Devolución rechazada (Fase 7). ReferenceId = id de la devolución.</summary>
    ReturnRejected = 5
}

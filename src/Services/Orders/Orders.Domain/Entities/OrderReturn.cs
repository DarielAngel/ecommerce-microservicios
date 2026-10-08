namespace Ecommerce.Orders.Domain.Entities;

public enum ReturnStatus
{
    /// <summary>El cliente la pidió; espera que el Admin decida.</summary>
    Requested = 0,

    /// <summary>
    /// El Admin la aprobó y el monto quedó fijado, pero PayPal todavía no confirmó el reembolso (por ejemplo,
    /// Pagos no respondió). Se puede reintentar: el reembolso usa el id de la devolución como clave.
    /// </summary>
    Approved = 1,

    /// <summary>El dinero ya se devolvió.</summary>
    Refunded = 2,

    /// <summary>El Admin la rechazó (con una nota para el cliente).</summary>
    Rejected = 3
}

public enum ReturnReason
{
    /// <summary>No le quedó bien (talla, medida).</summary>
    DoesNotFit = 0,

    /// <summary>Llegó dañado o con fallas.</summary>
    Damaged = 1,

    /// <summary>No es lo que pidió.</summary>
    WrongItem = 2,

    /// <summary>No es como se describía.</summary>
    NotAsDescribed = 3,

    /// <summary>Ya no lo quiere.</summary>
    ChangedMind = 4,

    /// <summary>Otro motivo (el comentario es obligatorio).</summary>
    Other = 5
}

/// <summary>
/// Una solicitud de devolución de (parte de) un pedido (Fase 7). Vive dentro del agregado <see cref="Order"/>:
/// todas las reglas (cuántas unidades se pueden devolver, cuánto dinero) dependen del pedido y de las otras
/// devoluciones, así que solo se cambia a través de los métodos de la orden.
/// </summary>
public class OrderReturn
{
    public const int MaxCommentLength = 500;

    private readonly List<OrderReturnLine> _lines = new();

    public Guid Id { get; private set; }

    /// <summary>
    /// true = no es una devolución sino la cancelación de un pedido pagado que todavía no se envió (todas las
    /// unidades, todo el dinero). Sigue el mismo camino: el Admin la aprueba y se reembolsa, o la rechaza.
    /// </summary>
    public bool IsCancellation { get; private set; }

    public ReturnStatus Status { get; private set; }
    public ReturnReason Reason { get; private set; }
    public string? Comment { get; private set; }

    /// <summary>Nota del Admin para el cliente (obligatoria al rechazar, opcional al aprobar).</summary>
    public string? AdminNote { get; private set; }

    /// <summary>Lo que se devuelve en dinero. Queda fijado al aprobar (antes es 0).</summary>
    public decimal RefundAmount { get; private set; }

    /// <summary>Puntos usados en la compra que se le devuelven al cliente. Se fija al aprobar.</summary>
    public int LoyaltyPointsToRestore { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Cuándo el Admin decidió (aprobó o rechazó).</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    public DateTime? RefundedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderReturnLine> Lines => _lines.AsReadOnly();

    public decimal ReturnedSubtotal => _lines.Sum(l => l.UnitPrice * l.Quantity);

    /// <summary>Abierta = todavía no terminó (pedida, o aprobada esperando el reembolso).</summary>
    public bool IsOpen => Status is ReturnStatus.Requested or ReturnStatus.Approved;

    /// <summary>¿Cuenta como devuelta (sus unidades ya no se pueden volver a pedir)?</summary>
    public bool HoldsUnits => Status != ReturnStatus.Rejected;

    private OrderReturn() { }

    internal OrderReturn(Guid id, ReturnReason reason, string? comment, IEnumerable<OrderReturnLine> lines, DateTime nowUtc,
        bool isCancellation = false)
    {
        Id = id;
        IsCancellation = isCancellation;
        Status = ReturnStatus.Requested;
        Reason = reason;
        Comment = comment;
        CreatedAtUtc = nowUtc;
        _lines.AddRange(lines);
    }

    internal void Approve(decimal refundAmount, int pointsToRestore, string? note, DateTime nowUtc)
    {
        Status = ReturnStatus.Approved;
        RefundAmount = refundAmount;
        LoyaltyPointsToRestore = pointsToRestore;
        AdminNote = note;
        ResolvedAtUtc = nowUtc;
    }

    internal void MarkRefunded(DateTime nowUtc)
    {
        Status = ReturnStatus.Refunded;
        RefundedAtUtc = nowUtc;
    }

    internal void Reject(string note, DateTime nowUtc)
    {
        Status = ReturnStatus.Rejected;
        AdminNote = note;
        ResolvedAtUtc = nowUtc;
    }
}

public class OrderReturnLine
{
    public Guid Id { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;

    /// <summary>Precio unitario de la compra (copiado de la línea del pedido).</summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    private OrderReturnLine() { }

    internal OrderReturnLine(Guid variantId, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        VariantId = variantId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}

/// <summary>Cuánto se devolvería con una devolución (para mostrarle al Admin antes de aprobar).</summary>
public readonly record struct RefundQuote(decimal Amount, int LoyaltyPointsToRestore, bool CompletesOrder);

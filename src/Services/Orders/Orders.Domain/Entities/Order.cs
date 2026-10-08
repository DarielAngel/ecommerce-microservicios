using Ecommerce.Orders.Domain.Enums;
using Ecommerce.Orders.Domain.Exceptions;

namespace Ecommerce.Orders.Domain.Entities;

public class Order
{
    private readonly List<OrderLine> _lines = new();
    private readonly List<OrderReturn> _returns = new();

    /// <summary>Días desde el envío en los que el cliente puede pedir una devolución (Fase 7).</summary>
    public const int ReturnWindowDays = 30;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string UserEmail { get; private set; } = null!;
    public string UserFullName { get; private set; } = null!;
    public string ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }

    /// <summary>Cuándo el Admin lo marcó como enviado (null en pedidos enviados antes de la Fase 5).</summary>
    public DateTime? ShippedAtUtc { get; private set; }

    /// <summary>Cuándo se canceló (Fase 7). Null si no está cancelado.</summary>
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Ventana de entrega estimada para mostrar al cliente (null si todavía no está pagado).</summary>
    public DeliveryEstimate? EstimatedDelivery => DeliveryEstimate.For(Status, PaidAtUtc, ShippedAtUtc ?? (Status == OrderStatus.Shipped ? UpdatedAtUtc : null));

    /// <summary>Código del cupón aplicado (ya normalizado por Promociones), o null si no hubo cupón.</summary>
    public string? CouponCode { get; private set; }

    /// <summary>Descuento del cupón, "congelado" al momento del checkout (lo calculó Promociones).</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Puntos de lealtad usados en esta compra (Fase 6); 0 si no se usaron.</summary>
    public int LoyaltyPoints { get; private set; }

    /// <summary>Lo que descontaron esos puntos (lo calculó Lealtad, "congelado" en el checkout).</summary>
    public decimal LoyaltyDiscount { get; private set; }

    public decimal Subtotal => _lines.Sum(l => l.LineTotal);

    /// <summary>Devoluciones pedidas sobre este pedido (Fase 7).</summary>
    public IReadOnlyCollection<OrderReturn> Returns => _returns.AsReadOnly();

    /// <summary>Lo que ya se devolvió en dinero.</summary>
    public decimal RefundedAmount => _returns.Where(r => r.Status == ReturnStatus.Refunded).Sum(r => r.RefundAmount);

    /// <summary>Lo que se cobra en PayPal: subtotal menos el cupón y menos los puntos.</summary>
    public decimal TotalAmount => Subtotal - DiscountAmount - LoyaltyDiscount;

    private Order() { }

    private Order(Guid id, Guid userId, string userEmail, string userFullName, string shippingAddress)
    {
        Id = id;
        UserId = userId;
        UserEmail = userEmail;
        UserFullName = userFullName;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.PendingPayment;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Crea la orden ya con sus líneas (checkout parcial: el caller decide qué ítems del
    /// carrito entran). La orden nace directamente en PendingPayment porque, para cuando se
    /// llama a este método, el stock YA se reservó con éxito (ver CheckoutCommandHandler) —
    /// no tendría sentido un estado "Created" transitorio que nadie observa.
    ///
    /// IMPORTANTE: recibe el `orderId` desde afuera (en vez de generarlo internamente) porque
    /// el caller ya tuvo que generarlo ANTES de llamar a este método, para poder reservar el
    /// stock en Inventario usándolo como clave de la reserva. Si generáramos el Id acá adentro,
    /// terminaríamos con dos Ids distintos para la misma orden — el de la reserva de stock y el
    /// de la orden persistida — y ConfirmPayment no podría confirmar/liberar la reserva correcta.
    ///
    /// Guarda también el email/nombre del comprador (tomados del JWT al momento del checkout)
    /// para no tener que volver a consultar a Users cuando haya que mandar notificaciones.
    /// </summary>
    public static Order Create(
        Guid orderId,
        Guid userId,
        string userEmail,
        string userFullName,
        string shippingAddress,
        IEnumerable<(Guid VariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity)> items,
        (string Code, decimal DiscountAmount)? coupon = null,
        (int Points, decimal DiscountAmount)? loyalty = null)
    {
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            throw new DomainException("La dirección de envío es obligatoria.");
        }

        var itemsList = items.ToList();

        if (itemsList.Count == 0)
        {
            throw new DomainException("La orden necesita al menos un ítem.");
        }

        if (itemsList.Any(i => i.Quantity <= 0))
        {
            throw new DomainException("Todas las cantidades deben ser mayores a cero.");
        }

        var order = new Order(orderId, userId, userEmail, userFullName, shippingAddress.Trim());

        foreach (var item in itemsList)
        {
            order._lines.Add(new OrderLine(
                item.VariantId, item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity));
        }

        if (coupon is { } c)
        {
            if (string.IsNullOrWhiteSpace(c.Code))
                throw new DomainException("El código del cupón es obligatorio.");
            // Defensa en profundidad: Promociones ya lo garantiza, pero una orden en $0 o negativa
            // no se puede cobrar, así que la orden tampoco la acepta.
            if (c.DiscountAmount <= 0 || c.DiscountAmount >= order.Subtotal)
                throw new DomainException("El descuento del cupón debe ser mayor que 0 y menor que el subtotal.");

            order.CouponCode = c.Code.Trim().ToUpperInvariant();
            order.DiscountAmount = c.DiscountAmount;
        }

        if (loyalty is { } l)
        {
            if (l.Points <= 0 || l.DiscountAmount <= 0)
                throw new DomainException("Los puntos usados y su descuento deben ser mayores que 0.");
            // Igual que con el cupón: nunca una orden en $0 o negativa.
            if (l.DiscountAmount >= order.Subtotal - order.DiscountAmount)
                throw new DomainException("El descuento por puntos debe ser menor que lo que queda por pagar.");

            order.LoyaltyPoints = l.Points;
            order.LoyaltyDiscount = l.DiscountAmount;
        }

        return order;
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException($"No se puede marcar como pagada una orden en estado '{Status}'.");
        }

        Status = OrderStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("No se puede marcar como fallida una orden que ya fue pagada.");
        }

        Status = OrderStatus.Failed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancela un pedido que todavía no se pagó (el cliente se arrepintió en el checkout). Quien llama libera el
    /// stock, el cupón y los puntos apartados. Un pedido ya pagado se cancela con <see cref="RequestCancellation"/>.
    /// </summary>
    public void MarkCancelled(DateTime? nowUtc = null)
    {
        if (Status == OrderStatus.Cancelled) return;
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException(Status == OrderStatus.Paid
                ? "No se puede cancelar una orden que ya fue pagada."
                : $"No se puede cancelar una orden en estado '{Status}'.");
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = nowUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = CancelledAtUtc.Value;
    }

    public void MarkShipped()
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException($"Solo se puede marcar como enviada una orden pagada (estado actual: '{Status}').");
        }

        if (OpenReturn is { IsCancellation: true })
        {
            throw new DomainException(
                "El cliente pidió cancelar este pedido: apruébala o recházala en Devoluciones antes de enviarlo.");
        }

        Status = OrderStatus.Shipped;
        ShippedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = ShippedAtUtc.Value;
    }

    // ---------------------------------------------------------------------------------------------
    // Devoluciones (Fase 7)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Hasta cuándo se puede pedir una devolución (null si el pedido todavía no se envió).</summary>
    public DateTime? ReturnDeadlineUtc =>
        Status is OrderStatus.Shipped or OrderStatus.Refunded
            // Pedidos enviados antes de la Fase 5 no guardaron la fecha de envío: se usa la del pago (nunca
            // UpdatedAtUtc, que cambia con cada devolución y correría el plazo).
            ? (ShippedAtUtc ?? PaidAtUtc ?? CreatedAtUtc).AddDays(ReturnWindowDays)
            : null;

    /// <summary>Unidades de esa variante que todavía se pueden devolver (descontadas las ya pedidas o devueltas).</summary>
    public int ReturnableQuantity(Guid variantId)
    {
        var bought = _lines.Where(l => l.VariantId == variantId).Sum(l => l.Quantity);
        var held = _returns.Where(r => r.HoldsUnits).SelectMany(r => r.Lines).Where(l => l.VariantId == variantId).Sum(l => l.Quantity);
        return Math.Max(bought - held, 0);
    }

    public OrderReturn? OpenReturn => _returns.FirstOrDefault(r => r.IsOpen);

    /// <summary>Por qué no se puede pedir una devolución ahora (null = sí se puede).</summary>
    public string? WhyCannotRequestReturn(DateTime nowUtc)
    {
        if (Status != OrderStatus.Shipped)
            return Status == OrderStatus.Refunded
                ? "Este pedido ya se devolvió completo."
                : "Solo se pueden devolver pedidos que ya fueron enviados.";
        if (nowUtc > ReturnDeadlineUtc)
            return $"El plazo para devolver este pedido ({ReturnWindowDays} días desde el envío) ya venció.";
        if (OpenReturn is not null)
            return "Ya hay una devolución en curso para este pedido; espera a que se resuelva.";
        if (_lines.All(l => ReturnableQuantity(l.VariantId) == 0))
            return "Ya pediste la devolución de todos los productos de este pedido.";
        return null;
    }

    public OrderReturn RequestReturn(
        IEnumerable<(Guid VariantId, int Quantity)> items, ReturnReason reason, string? comment, DateTime nowUtc)
    {
        if (WhyCannotRequestReturn(nowUtc) is { } why) throw new DomainException(why);

        var cleanComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (cleanComment?.Length > OrderReturn.MaxCommentLength)
            throw new DomainException($"El comentario admite hasta {OrderReturn.MaxCommentLength} caracteres.");
        if (reason == ReturnReason.Other && cleanComment is null)
            throw new DomainException("Cuéntanos el motivo de la devolución.");
        if (!Enum.IsDefined(reason))
            throw new DomainException("Motivo de devolución inválido.");

        var requested = items
            .Where(i => i.Quantity != 0)
            .GroupBy(i => i.VariantId)
            .Select(g => (VariantId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();
        if (requested.Count == 0)
            throw new DomainException("Elige al menos un producto para devolver.");

        var lines = new List<OrderReturnLine>();
        foreach (var (variantId, quantity) in requested)
        {
            var line = _lines.FirstOrDefault(l => l.VariantId == variantId)
                ?? throw new DomainException("Uno de los productos no es de este pedido.");
            if (quantity < 0)
                throw new DomainException("Las cantidades deben ser mayores a cero.");
            var available = ReturnableQuantity(variantId);
            if (quantity > available)
                throw new DomainException(available == 0
                    ? $"Ya pediste la devolución de todas las unidades de {line.ProductName}."
                    : $"De {line.ProductName} puedes devolver como máximo {available}.");
            lines.Add(new OrderReturnLine(line.VariantId, line.ProductId, line.ProductName, line.UnitPrice, quantity));
        }

        var orderReturn = new OrderReturn(Guid.NewGuid(), reason, cleanComment, lines, nowUtc);
        _returns.Add(orderReturn);
        UpdatedAtUtc = nowUtc;
        return orderReturn;
    }

    /// <summary>Por qué no se puede pedir la cancelación ahora (null = sí se puede).</summary>
    public string? WhyCannotCancel()
    {
        if (Status == OrderStatus.PendingPayment) return null;
        if (Status != OrderStatus.Paid)
            return Status is OrderStatus.Shipped
                ? "Este pedido ya fue enviado: en vez de cancelarlo, pide una devolución."
                : $"No se puede cancelar un pedido en estado '{Status}'.";
        if (OpenReturn is not null)
            return "Ya pediste cancelar este pedido; espera a que lo revisemos.";
        return null;
    }

    /// <summary>
    /// El pedido está pagado pero no enviado: se pide la cancelación de TODO el pedido (Fase 7). Funciona como una
    /// devolución de todas las unidades: el Admin la aprueba (y se reembolsa todo) o la rechaza. Mientras está
    /// pendiente, el pedido no se puede enviar.
    /// </summary>
    public OrderReturn RequestCancellation(ReturnReason reason, string? comment, DateTime nowUtc)
    {
        if (Status != OrderStatus.Paid || OpenReturn is not null)
            throw new DomainException(WhyCannotCancel() ?? "No se puede cancelar este pedido.");
        if (!Enum.IsDefined(reason))
            throw new DomainException("Motivo inválido.");
        var cleanComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (cleanComment?.Length > OrderReturn.MaxCommentLength)
            throw new DomainException($"El comentario admite hasta {OrderReturn.MaxCommentLength} caracteres.");

        var lines = _lines.Select(l => new OrderReturnLine(l.VariantId, l.ProductId, l.ProductName, l.UnitPrice, l.Quantity));
        var cancellation = new OrderReturn(Guid.NewGuid(), reason, cleanComment, lines, nowUtc, isCancellation: true);
        _returns.Add(cancellation);
        UpdatedAtUtc = nowUtc;
        return cancellation;
    }

    private OrderReturn GetReturn(Guid returnId) =>
        _returns.FirstOrDefault(r => r.Id == returnId) ?? throw new DomainException("La devolución no es de este pedido.");

    /// <summary>
    /// Cuánto dinero (y cuántos puntos usados) se devuelven con esta devolución: la parte proporcional de lo que
    /// el cliente realmente pagó, porque el cupón y los puntos se reparten entre todos los productos. La devolución
    /// que completa el pedido se lleva exactamente lo que queda, así la suma nunca pasa de lo cobrado (ni queda
    /// un centavo suelto por redondeo).
    /// </summary>
    public RefundQuote QuoteRefund(Guid returnId)
    {
        var target = GetReturn(returnId);
        if (target.Status is ReturnStatus.Approved or ReturnStatus.Refunded)
            return new RefundQuote(target.RefundAmount, target.LoyaltyPointsToRestore, CompletesWith(target));

        var committed = _returns.Where(r => r.Id != target.Id && r.Status is ReturnStatus.Approved or ReturnStatus.Refunded).ToList();
        var amountLeft = TotalAmount - committed.Sum(r => r.RefundAmount);
        var pointsLeft = LoyaltyPoints - committed.Sum(r => r.LoyaltyPointsToRestore);
        var completes = CompletesWith(target);

        if (completes) return new RefundQuote(amountLeft, pointsLeft, true);

        var share = Subtotal == 0 ? 0m : target.ReturnedSubtotal / Subtotal;
        var amount = Math.Min(decimal.Round(TotalAmount * share, 2, MidpointRounding.AwayFromZero), amountLeft);
        var points = Math.Min((int)Math.Floor(LoyaltyPoints * share), pointsLeft);
        return new RefundQuote(amount, points, false);
    }

    /// <summary>¿Con esta devolución quedan devueltas (aprobadas o reembolsadas) todas las unidades del pedido?</summary>
    private bool CompletesWith(OrderReturn target)
    {
        var settled = _returns
            .Where(r => r.Id == target.Id || r.Status is ReturnStatus.Approved or ReturnStatus.Refunded)
            .SelectMany(r => r.Lines)
            .Sum(l => l.Quantity);
        return settled >= _lines.Sum(l => l.Quantity);
    }

    /// <summary>
    /// El Admin la aprueba: el monto queda fijado. Idempotente: aprobar de nuevo una ya aprobada (para reintentar
    /// el reembolso) no cambia nada.
    /// </summary>
    public OrderReturn ApproveReturn(Guid returnId, string? note, DateTime nowUtc)
    {
        var target = GetReturn(returnId);
        switch (target.Status)
        {
            case ReturnStatus.Approved:
            case ReturnStatus.Refunded:
                return target;
            case ReturnStatus.Rejected:
                throw new DomainException("Esa devolución ya fue rechazada.");
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote?.Length > OrderReturn.MaxCommentLength)
            throw new DomainException($"La nota admite hasta {OrderReturn.MaxCommentLength} caracteres.");
        if (target.IsCancellation && Status != OrderStatus.Paid)
            throw new DomainException("Este pedido ya no se puede cancelar.");

        var quote = QuoteRefund(returnId);
        if (quote.Amount <= 0)
            throw new DomainException("No queda dinero por devolver en este pedido.");

        target.Approve(quote.Amount, quote.LoyaltyPointsToRestore, cleanNote, nowUtc);
        UpdatedAtUtc = nowUtc;
        return target;
    }

    /// <summary>PayPal confirmó el reembolso. Si con esto se devolvió todo el pedido, el pedido pasa a Refunded.</summary>
    public void MarkReturnRefunded(Guid returnId, DateTime nowUtc)
    {
        var target = GetReturn(returnId);
        if (target.Status == ReturnStatus.Refunded) return;
        if (target.Status != ReturnStatus.Approved)
            throw new DomainException("Solo se puede reembolsar una devolución aprobada.");

        target.MarkRefunded(nowUtc);
        if (target.IsCancellation && Status == OrderStatus.Paid)
        {
            Status = OrderStatus.Cancelled;
            CancelledAtUtc = nowUtc;
            UpdatedAtUtc = nowUtc;
            return;
        }

        var refundedUnits = _returns.Where(r => r.Status == ReturnStatus.Refunded).SelectMany(r => r.Lines).Sum(l => l.Quantity);
        if (refundedUnits >= _lines.Sum(l => l.Quantity)) Status = OrderStatus.Refunded;
        UpdatedAtUtc = nowUtc;
    }

    /// <param name="approvedWithoutRefund">
    /// true = quien llama verificó con Pagos que el reembolso de esta devolución aprobada NO existe, así que se
    /// puede cancelar (por ejemplo, PayPal lo rechaza siempre y si no, el pedido quedaría trabado).
    /// </param>
    public OrderReturn RejectReturn(Guid returnId, string note, DateTime nowUtc, bool approvedWithoutRefund = false)
    {
        var target = GetReturn(returnId);
        if (target.Status == ReturnStatus.Rejected) return target;
        if (target.Status == ReturnStatus.Refunded)
            throw new DomainException("Esa devolución ya fue reembolsada; no se puede rechazar.");
        if (target.Status == ReturnStatus.Approved && !approvedWithoutRefund)
            throw new DomainException("Esa devolución ya fue aprobada; no se puede rechazar.");
        if (string.IsNullOrWhiteSpace(note))
            throw new DomainException("Escribe una nota para el cliente explicando por qué se rechaza.");
        if (note.Trim().Length > OrderReturn.MaxCommentLength)
            throw new DomainException($"La nota admite hasta {OrderReturn.MaxCommentLength} caracteres.");

        target.Reject(note.Trim(), nowUtc);
        UpdatedAtUtc = nowUtc;
        return target;
    }
}

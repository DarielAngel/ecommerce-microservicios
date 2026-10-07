using Ecommerce.Cart.Domain.Exceptions;

namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// Un carrito por usuario logueado (no hay carrito de invitado). Es la raíz de agregado:
/// toda modificación a sus líneas pasa por acá, nunca se manipulan CartItem sueltos desde afuera.
/// </summary>
public class Cart
{
    private readonly List<CartItem> _items = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Email y nombre del dueño (tomados de su token al agregar productos), para el recordatorio.</summary>
    public string? ContactEmail { get; private set; }
    public string? ContactName { get; private set; }

    /// <summary>
    /// La "última actividad" (UpdatedAtUtc) del carrito que ya se le recordó. Si el cliente vuelve y lo
    /// cambia, UpdatedAtUtc cambia y deja de coincidir: se le puede recordar de nuevo más adelante.
    /// Comparar contra la actividad (y no contra la hora del envío) no depende de relojes desfasados.
    /// </summary>
    public DateTime? AbandonedReminderForActivityAtUtc { get; private set; }

    public decimal Subtotal => _items.Sum(i => i.LineTotal);
    public int TotalItemCount => _items.Sum(i => i.Quantity);

    private Cart() { }

    private Cart(Guid userId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static Cart CreateForUser(Guid userId) => new(userId);

    /// <summary>
    /// Agrega una cantidad al carrito. Si la variante ya está en el carrito, suma la cantidad
    /// a la línea existente y CONSERVA el precio original de esa línea (no lo actualiza al
    /// precio actual pasado aquí) — así el precio queda consistentemente "congelado" desde
    /// la primera vez que se agregó.
    /// </summary>
    /// <returns>
    /// El CartItem recién creado si la variante era nueva en el carrito; null si solo se
    /// incrementó la cantidad de una línea existente. El caller (repositorio) necesita saber
    /// esto para marcar explícitamente el ítem nuevo como "Added" ante EF Core — igual que tuvimos
    /// que hacer con las imágenes de producto en Catálogo, agregar un hijo a un agregado ya
    /// cargado no siempre se detecta solo.
    /// </returns>
    public CartItem? AddItem(Guid variantId, Guid productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("La cantidad a agregar debe ser mayor a cero.");
        }

        var existingItem = _items.FirstOrDefault(i => i.VariantId == variantId);
        CartItem? newItem = null;

        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            newItem = new CartItem(variantId, productId, productName, sku, unitPrice, quantity);
            _items.Add(newItem);
        }

        UpdatedAtUtc = DateTime.UtcNow;
        return newItem;
    }

    /// <summary>Fija la cantidad de una línea existente a un valor absoluto (0 la elimina).</summary>
    public void SetItemQuantity(Guid variantId, int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("La cantidad no puede ser negativa.");
        }

        var item = _items.FirstOrDefault(i => i.VariantId == variantId)
            ?? throw new DomainException("Esa variante no está en el carrito.");

        if (quantity == 0)
        {
            _items.Remove(item);
        }
        else
        {
            item.SetQuantity(quantity);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveItem(Guid variantId)
    {
        var item = _items.FirstOrDefault(i => i.VariantId == variantId)
            ?? throw new DomainException("Esa variante no está en el carrito.");

        _items.Remove(item);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        _items.Clear();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Guarda a quién escribirle si deja el carrito abandonado (lo último que llegó en su token).</summary>
    public void SetContact(string? email, string? fullName)
    {
        if (!string.IsNullOrWhiteSpace(email)) ContactEmail = email.Trim();
        if (!string.IsNullOrWhiteSpace(fullName)) ContactName = fullName.Trim();
    }

    /// <summary>
    /// ¿Hay que recordarle al cliente este carrito? Sí cuando: tiene productos y a quién escribir; lleva al
    /// menos <paramref name="idleFor"/> sin cambios pero no más de <paramref name="maxAge"/> (no escribimos
    /// por carritos de hace semanas); y todavía no se le recordó desde su último cambio.
    /// </summary>
    public bool NeedsAbandonedReminder(DateTime nowUtc, TimeSpan idleFor, TimeSpan maxAge) =>
        _items.Count > 0
        && ContactEmail is not null
        && UpdatedAtUtc <= nowUtc - idleFor
        && UpdatedAtUtc >= nowUtc - maxAge
        && AbandonedReminderForActivityAtUtc != UpdatedAtUtc;

    /// <summary>No cambia UpdatedAtUtc: recordar no es actividad del cliente.</summary>
    public void MarkAbandonedReminderSent() => AbandonedReminderForActivityAtUtc = UpdatedAtUtc;

    /// <summary>Si el aviso no se pudo publicar, se deshace la marca para reintentar en la próxima vuelta.</summary>
    public void UndoAbandonedReminder(DateTime? previous) => AbandonedReminderForActivityAtUtc = previous;

    /// <summary>Cantidad que ya hay en el carrito para una variante (0 si no está).</summary>
    public int GetCurrentQuantity(Guid variantId) =>
        _items.FirstOrDefault(i => i.VariantId == variantId)?.Quantity ?? 0;
}

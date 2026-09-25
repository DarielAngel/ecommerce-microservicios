namespace Ecommerce.Contracts.Events;

/// <summary>
/// Publicado por Catálogo (vía RabbitMQ) cada vez que se crea una variante de producto nueva.
/// Inventario lo consume para crear el registro de stock correspondiente, en 0, listo para
/// que un Admin lo ajuste. Este es el único "idioma común" entre ambos servicios: ninguno
/// conoce la base de datos ni las entidades internas del otro.
/// </summary>
public record VariantCreatedEvent(
    Guid VariantId,
    Guid ProductId,
    string Sku,
    DateTime OccurredAtUtc);

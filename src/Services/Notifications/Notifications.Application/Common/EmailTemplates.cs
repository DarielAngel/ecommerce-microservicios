using System.Globalization;
using System.Net;

namespace Ecommerce.Notifications.Application.Common;

/// <summary>
/// Plantillas simples de email. IMPORTANTE: todo valor que venga del usuario (nombre, etc.)
/// pasa por HtmlEncode antes de insertarse en el HTML — si no, un nombre como
/// "&lt;script&gt;..." terminaría inyectado dentro del email (HTML injection).
/// </summary>
public static class EmailTemplates
{
    public static string ShortOrderId(Guid orderId) => orderId.ToString("N")[..8].ToUpperInvariant();

    private static string Wrap(string title, string bodyHtml) => $"""
        <div style="font-family: Arial, sans-serif; max-width: 560px; margin: 0 auto; color: #1f2937;">
          <h2 style="color: #111827;">{title}</h2>
          {bodyHtml}
          <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
          <p style="font-size: 12px; color: #6b7280;">Este es un mensaje automático, por favor no respondas a este correo.</p>
        </div>
        """;

    public static (string Subject, string Html) Welcome(string fullName)
    {
        var name = WebUtility.HtmlEncode(fullName);
        return (
            "¡Bienvenido/a a nuestra tienda!",
            Wrap("¡Bienvenido/a a nuestra tienda!",
                $"<p>Hola <b>{name}</b>, tu cuenta se creó correctamente. Ya puedes explorar el catálogo y hacer tu primera compra.</p>"));
    }

    public static (string Subject, string Html) OrderPaid(Guid orderId, string fullName, decimal total, string currency)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var shortId = ShortOrderId(orderId);
        var amount = total.ToString("F2", CultureInfo.InvariantCulture);
        return (
            $"Confirmación de tu pedido #{shortId}",
            Wrap("¡Gracias por tu compra!",
                $"<p>Hola <b>{name}</b>, recibimos tu pago correctamente.</p>" +
                $"<p><b>Pedido:</b> #{shortId}<br/><b>Total pagado:</b> {amount} {WebUtility.HtmlEncode(currency)}</p>" +
                "<p>Te avisaremos por este medio cuando tu pedido sea enviado.</p>"));
    }

    public static (string Subject, string Html) OrderShipped(Guid orderId, string fullName)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var shortId = ShortOrderId(orderId);
        return (
            $"Tu pedido #{shortId} va en camino",
            Wrap("¡Tu pedido va en camino!",
                $"<p>Hola <b>{name}</b>, tu pedido <b>#{shortId}</b> ya fue enviado.</p>"));
    }

    public record CartLine(string ProductName, int Quantity, decimal UnitPrice);

    /// <summary>
    /// Recordatorio de carrito abandonado: lista lo que dejó (hasta 5 productos) y un botón al carrito.
    /// Tono amable, sin presión artificial (nada de "¡quedan pocas unidades!" inventado).
    /// </summary>
    public static (string Subject, string Html) CartAbandoned(
        string fullName, IReadOnlyList<CartLine> items, decimal subtotal, string cartUrl)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var rows = string.Concat(items.Take(5).Select(i =>
            $"<tr><td style=\"padding: 4px 0;\">{i.Quantity}× {WebUtility.HtmlEncode(i.ProductName)}</td>" +
            $"<td style=\"padding: 4px 0; text-align: right;\">{(i.UnitPrice * i.Quantity).ToString("F2", CultureInfo.InvariantCulture)} USD</td></tr>"));
        var more = items.Count > 5 ? $"<p style=\"color: #6b7280;\">…y {items.Count - 5} producto(s) más.</p>" : "";
        var url = WebUtility.HtmlEncode(cartUrl);
        return (
            "Dejaste productos en tu carrito",
            Wrap("¿Te olvidaste de algo?",
                $"<p>Hola <b>{name}</b>, guardamos tu carrito por si quieres terminar la compra:</p>" +
                $"<table style=\"width: 100%; border-collapse: collapse;\">{rows}</table>{more}" +
                $"<p><b>Subtotal:</b> {subtotal.ToString("F2", CultureInfo.InvariantCulture)} USD</p>" +
                $"<p><a href=\"{url}\" style=\"display: inline-block; background: #059669; color: #ffffff; padding: 10px 18px; " +
                "border-radius: 9999px; text-decoration: none;\">Volver a mi carrito</a></p>" +
                "<p style=\"font-size: 12px; color: #6b7280;\">Los precios y el stock pueden cambiar hasta que completes la compra.</p>"));
    }
}

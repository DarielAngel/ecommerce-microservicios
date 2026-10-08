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

    public record RefundLine(string ProductName, int Quantity);

    /// <summary>Devolución reembolsada (Fase 7): qué se devolvió, cuánto vuelve y cuándo se ve en PayPal.</summary>
    public static (string Subject, string Html) ReturnRefunded(
        Guid orderId, string fullName, IReadOnlyList<RefundLine> items, decimal amount, string currency, bool orderFullyRefunded,
        int pointsRestored, bool orderCancelled = false)
    {
        if (orderCancelled) return OrderCancelled(orderId, fullName, amount, currency, pointsRestored);

        var name = WebUtility.HtmlEncode(fullName);
        var shortId = ShortOrderId(orderId);
        var rows = string.Concat(items.Select(i => $"<li>{i.Quantity}× {WebUtility.HtmlEncode(i.ProductName)}</li>"));
        var money = $"{amount.ToString("F2", CultureInfo.InvariantCulture)} {WebUtility.HtmlEncode(currency)}";
        var points = pointsRestored > 0
            ? $"<p>También te devolvimos <b>{pointsRestored} puntos</b> que habías usado en esa compra.</p>"
            : "";
        return (
            $"Reembolso de tu pedido #{shortId}",
            Wrap("Tu reembolso está en camino",
                $"<p>Hola <b>{name}</b>, aprobamos la devolución de tu pedido <b>#{shortId}</b>:</p>" +
                $"<ul>{rows}</ul>" +
                $"<p><b>Te devolvemos:</b> {money}{(orderFullyRefunded ? " (el pedido completo)" : "")}</p>" +
                points +
                "<p style=\"font-size: 12px; color: #6b7280;\">El dinero vuelve al mismo medio de pago en PayPal; puede tardar unos días en verse reflejado.</p>"));
    }

    /// <summary>Pedido pagado que se canceló antes del envío (Fase 7): se reembolsó todo.</summary>
    public static (string Subject, string Html) OrderCancelled(
        Guid orderId, string fullName, decimal amount, string currency, int pointsRestored)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var shortId = ShortOrderId(orderId);
        var money = $"{amount.ToString("F2", CultureInfo.InvariantCulture)} {WebUtility.HtmlEncode(currency)}";
        var points = pointsRestored > 0
            ? $"<p>También te devolvimos los <b>{pointsRestored} puntos</b> que habías usado.</p>"
            : "";
        return (
            $"Cancelamos tu pedido #{shortId}",
            Wrap("Tu pedido fue cancelado",
                $"<p>Hola <b>{name}</b>, cancelamos tu pedido <b>#{shortId}</b> antes de enviarlo.</p>" +
                $"<p><b>Te devolvemos:</b> {money} (todo lo que pagaste)</p>" +
                points +
                "<p style=\"font-size: 12px; color: #6b7280;\">El dinero vuelve al mismo medio de pago en PayPal; puede tardar unos días en verse reflejado.</p>"));
    }

    /// <summary>Devolución rechazada (Fase 7), con la explicación del Admin.</summary>
    public static (string Subject, string Html) ReturnRejected(Guid orderId, string fullName, string note, bool isCancellation = false)
    {
        var name = WebUtility.HtmlEncode(fullName);
        var shortId = ShortOrderId(orderId);
        var what = isCancellation ? "cancelación" : "devolución";
        return (
            $"Sobre la {what} de tu pedido #{shortId}",
            Wrap($"No pudimos aceptar tu {what}",
                $"<p>Hola <b>{name}</b>, revisamos la {what} que pediste del pedido <b>#{shortId}</b> y no la pudimos aceptar.</p>" +
                $"<p><b>Motivo:</b> {WebUtility.HtmlEncode(note)}</p>" +
                "<p>Si crees que es un error, responde desde la sección de ayuda de la tienda y lo revisamos.</p>"));
    }
}

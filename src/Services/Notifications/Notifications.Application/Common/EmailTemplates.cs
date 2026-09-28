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
}

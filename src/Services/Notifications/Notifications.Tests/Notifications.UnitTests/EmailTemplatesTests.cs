using Ecommerce.Notifications.Application.Common;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Notifications.UnitTests;

public class EmailTemplatesTests
{
    [Fact]
    public void Welcome_ConNombreMalicioso_DeberiaEscaparElHtml()
    {
        var (_, html) = EmailTemplates.Welcome("<script>alert('x')</script>");

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void OrderPaid_DeberiaIncluirIdCortoYTotalConDosDecimales()
    {
        var orderId = Guid.Parse("abcdef12-0000-0000-0000-000000000000");

        var (subject, html) = EmailTemplates.OrderPaid(orderId, "Ana", 49.5m, "USD");

        subject.Should().Contain("#ABCDEF12");
        html.Should().Contain("49.50 USD");
    }

    [Fact]
    public void OrderShipped_DeberiaIncluirIdCortoEnElAsunto()
    {
        var orderId = Guid.Parse("12345678-0000-0000-0000-000000000000");

        var (subject, _) = EmailTemplates.OrderShipped(orderId, "Ana");

        subject.Should().Contain("#12345678");
    }

    [Fact]
    public void OrderPaid_ConMonedaMaliciosa_DeberiaEscaparla()
    {
        var (_, html) = EmailTemplates.OrderPaid(Guid.NewGuid(), "Ana", 10m, "<b>X</b>");

        html.Should().NotContain("<b>X</b>");
    }

    [Fact]
    public void CartAbandoned_ListaLoQueDejo_ConEnlaceAlCarrito_YEscapaLosNombres()
    {
        var items = new List<EmailTemplates.CartLine>
        {
            new("Taza <b>rota</b>", 2, 8.5m),
            new("Plato", 1, 5m)
        };

        var (subject, html) = EmailTemplates.CartAbandoned("Ana & Luis", items, 22m, "http://localhost:5173/cart");

        subject.Should().Be("Dejaste productos en tu carrito");
        html.Should().Contain("2× Taza &lt;b&gt;rota&lt;/b&gt;");
        html.Should().Contain("17.00 USD");
        html.Should().Contain("<b>Subtotal:</b> 22.00 USD");
        html.Should().Contain("href=\"http://localhost:5173/cart\"");
        html.Should().Contain("Ana &amp; Luis");
    }

    [Fact]
    public void CartAbandoned_ConMuchosProductos_MuestraCincoYCuantosMas()
    {
        var items = Enumerable.Range(1, 7).Select(i => new EmailTemplates.CartLine($"Producto {i}", 1, 1m)).ToList();

        var (_, html) = EmailTemplates.CartAbandoned("Ana", items, 7m, "http://x/cart");

        html.Should().Contain("Producto 5").And.NotContain("Producto 6");
        html.Should().Contain("…y 2 producto(s) más.");
    }
}

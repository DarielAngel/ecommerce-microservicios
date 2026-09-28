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
}

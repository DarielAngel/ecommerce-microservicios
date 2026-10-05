using Ecommerce.Reviews.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Reviews.UnitTests;

public class RatingSummaryTests
{
    [Fact]
    public void FromDistribution_SinReseñas_DeberiaDarPromedioCeroYDistribucionCompletaEnCero()
    {
        var productId = Guid.NewGuid();

        var summary = RatingSummary.FromDistribution(productId, new Dictionary<int, int>());

        summary.ProductId.Should().Be(productId);
        summary.Count.Should().Be(0);
        summary.Average.Should().Be(0);
        summary.Distribution.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        summary.Distribution.Values.Should().OnlyContain(v => v == 0);
    }

    [Fact]
    public void FromDistribution_DeberiaCalcularElPromedioRedondeadoAUnDecimal()
    {
        // 5,5,4 -> (10 + 4) / 3 = 4.666... -> 4.7
        var summary = RatingSummary.FromDistribution(Guid.NewGuid(), new Dictionary<int, int> { [5] = 2, [4] = 1 });

        summary.Count.Should().Be(3);
        summary.Average.Should().Be(4.7);
    }

    [Fact]
    public void FromDistribution_DeberiaCompletarLasEstrellasQueNoLlegaron()
    {
        var summary = RatingSummary.FromDistribution(Guid.NewGuid(), new Dictionary<int, int> { [5] = 1 });

        summary.Distribution[5].Should().Be(1);
        summary.Distribution[4].Should().Be(0);
        summary.Distribution[1].Should().Be(0);
    }

    [Fact]
    public void FromDistribution_ConUnaSolaCalificacion_DeberiaDarEsaCalificacionComoPromedio()
    {
        RatingSummary.FromDistribution(Guid.NewGuid(), new Dictionary<int, int> { [3] = 4 }).Average.Should().Be(3.0);
    }

    [Fact]
    public void FromDistribution_DeberiaIgnorarEstrellasFueraDeRango()
    {
        var summary = RatingSummary.FromDistribution(Guid.NewGuid(), new Dictionary<int, int> { [5] = 1, [9] = 100, [0] = 50 });

        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5.0);
        summary.Distribution.Keys.Should().NotContain(new[] { 0, 9 });
    }
}

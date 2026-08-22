using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Rounding-table tests for TaxCalculator.SplitInclusive: net + tax must always sum
/// exactly to the gross, and the tax component is gross * rate / (100 + rate) rounded
/// to the cent with MidpointRounding.AwayFromZero. The rate=10 cases mirror the retired
/// GstCalculator's gross/11 behaviour exactly (research.md's compatibility requirement).
/// </summary>
public class TaxCalculatorTests
{
    [Theory]
    [InlineData(110.00, 10, 10.00, 100.00)]
    [InlineData(11.00, 10, 1.00, 10.00)]
    [InlineData(1.00, 10, 0.09, 0.91)]
    [InlineData(0.11, 10, 0.01, 0.10)]
    [InlineData(100.00, 10, 9.09, 90.91)]
    [InlineData(55.00, 10, 5.00, 50.00)]
    [InlineData(0.055, 10, 0.01, 0.045)]
    public void SplitInclusive_ReturnsExpectedNetAndTax_AtTenPercent(decimal gross, decimal ratePercent, decimal expectedTax, decimal expectedNet)
    {
        var (net, tax) = TaxCalculator.SplitInclusive(gross, ratePercent);

        Assert.Equal(expectedTax, tax);
        Assert.Equal(expectedNet, net);
    }

    [Theory]
    [InlineData(115.00, 15, 15.00, 100.00)]
    [InlineData(120.00, 20, 20.00, 100.00)]
    [InlineData(107.50, 7.5, 7.50, 100.00)]
    public void SplitInclusive_ReturnsExpectedNetAndTax_AtOtherRates(decimal gross, decimal ratePercent, decimal expectedTax, decimal expectedNet)
    {
        var (net, tax) = TaxCalculator.SplitInclusive(gross, ratePercent);

        Assert.Equal(expectedTax, tax);
        Assert.Equal(expectedNet, net);
    }

    [Theory]
    [InlineData(110.00, 10)]
    [InlineData(11.00, 10)]
    [InlineData(1.00, 10)]
    [InlineData(0.11, 10)]
    [InlineData(100.00, 10)]
    [InlineData(55.00, 10)]
    [InlineData(33.33, 15)]
    [InlineData(0.01, 20)]
    public void SplitInclusive_NetPlusTax_AlwaysEqualsGross(decimal gross, decimal ratePercent)
    {
        var (net, tax) = TaxCalculator.SplitInclusive(gross, ratePercent);

        Assert.Equal(gross, net + tax);
    }

    [Fact]
    public void SplitInclusive_ZeroGross_ReturnsZeroNetAndZeroTax()
    {
        var (net, tax) = TaxCalculator.SplitInclusive(0m, 10m);

        Assert.Equal(0m, net);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void SplitInclusive_ZeroRate_ReturnsZeroTax_AndNetEqualsGross()
    {
        var (net, tax) = TaxCalculator.SplitInclusive(100m, 0m);

        Assert.Equal(0m, tax);
        Assert.Equal(100m, net);
    }

    [Fact]
    public void SplitInclusive_RoundsMidpointAwayFromZero()
    {
        // 0.055 at 10%: gross*10/110 = 0.005 exactly -> rounds to 0.01 (away from zero)
        var (_, tax) = TaxCalculator.SplitInclusive(0.055m, 10m);

        Assert.Equal(0.01m, tax);
    }
}

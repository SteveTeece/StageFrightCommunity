using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Rounding-table tests for GstCalculator.SplitInclusive: net + gst must always sum
/// exactly to the gross, and the gst component is gross/11 rounded to the cent with
/// MidpointRounding.AwayFromZero.
/// </summary>
public class GstCalculatorTests
{
    [Theory]
    [InlineData(110.00, 10.00, 100.00)]
    [InlineData(11.00, 1.00, 10.00)]
    [InlineData(1.00, 0.09, 0.91)]
    [InlineData(0.11, 0.01, 0.10)]
    [InlineData(100.00, 9.09, 90.91)]
    [InlineData(55.00, 5.00, 50.00)]
    [InlineData(0.055, 0.01, 0.045)]
    public void SplitInclusive_ReturnsExpectedNetAndGst(decimal gross, decimal expectedGst, decimal expectedNet)
    {
        var (net, gst) = GstCalculator.SplitInclusive(gross);

        Assert.Equal(expectedGst, gst);
        Assert.Equal(expectedNet, net);
    }

    [Theory]
    [InlineData(110.00)]
    [InlineData(11.00)]
    [InlineData(1.00)]
    [InlineData(0.11)]
    [InlineData(100.00)]
    [InlineData(55.00)]
    [InlineData(33.33)]
    [InlineData(0.01)]
    public void SplitInclusive_NetPlusGst_AlwaysEqualsGross(decimal gross)
    {
        var (net, gst) = GstCalculator.SplitInclusive(gross);

        Assert.Equal(gross, net + gst);
    }

    [Fact]
    public void SplitInclusive_ZeroGross_ReturnsZeroNetAndZeroGst()
    {
        var (net, gst) = GstCalculator.SplitInclusive(0m);

        Assert.Equal(0m, net);
        Assert.Equal(0m, gst);
    }

    [Fact]
    public void SplitInclusive_RoundsMidpointAwayFromZero()
    {
        // 0.055 rounded to 2dp midpoint case: gross/11 = 0.005 exactly -> rounds to 0.01 (away from zero)
        var (_, gst) = GstCalculator.SplitInclusive(0.055m);

        Assert.Equal(0.01m, gst);
    }
}

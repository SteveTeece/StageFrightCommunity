using StageFright.Core.Enums;
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

    // --- spec 028 FR-005: rounding follows the configured currency's minor unit (0, 2 or 3 digits) ---

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void SplitInclusive_NetPlusTax_AlwaysEqualsGross_AtAnyMinorUnitDigits(int minorUnitDigits)
    {
        decimal[] grosses = [110m, 1000.555m, 12345.678m, 0.001m, 99.999m, 7m];

        foreach (var gross in grosses)
        {
            var (net, tax) = TaxCalculator.SplitInclusive(gross, 12.5m, minorUnitDigits);
            Assert.Equal(gross, net + tax);
        }
    }

    [Theory]
    [InlineData(1000.50, 10, 0, 91)]      // yen-style: gross*10/110 = 90.95… -> 91 whole units
    [InlineData(110.00, 10, 2, 10.00)]    // cent-style: unchanged 2-digit behaviour
    [InlineData(110.000, 10, 3, 10.000)]  // dinar-style: 3-digit minor unit
    [InlineData(1.234, 10, 3, 0.112)]     // 1.234*10/110 = 0.11218… -> 0.112 at 3 digits
    public void SplitInclusive_RoundsTaxToConfiguredMinorUnit(decimal gross, decimal ratePercent, int minorUnitDigits, decimal expectedTax)
    {
        var (net, tax) = TaxCalculator.SplitInclusive(gross, ratePercent, minorUnitDigits);

        Assert.Equal(expectedTax, tax);
        Assert.Equal(gross - expectedTax, net);
        Assert.Equal(gross, net + tax);
    }

    [Fact]
    public void SplitInclusive_DefaultsToTwoMinorUnitDigits_WhenNotSpecified()
    {
        var withDefault = TaxCalculator.SplitInclusive(100m, 10m);
        var withExplicitTwo = TaxCalculator.SplitInclusive(100m, 10m, 2);

        Assert.Equal(withExplicitTwo, withDefault);
    }

    // --- issue #354: SplitExclusive — the entered amount is the net, tax is added on top ---

    [Theory]
    [InlineData(100.00, 8, 8.00, 108.00)]     // acceptance fixture: net 100 @ 8% -> tax 8, gross 108
    [InlineData(100.00, 10, 10.00, 110.00)]
    [InlineData(50.00, 10, 5.00, 55.00)]
    [InlineData(0.06, 10, 0.01, 0.07)]        // 0.006 -> rounds to 0.01 away from zero
    [InlineData(12.34, 8.5, 1.05, 13.39)]     // 12.34 * 0.085 = 1.0489 -> 1.05
    public void SplitExclusive_ReturnsExpectedGrossAndTax(decimal net, decimal ratePercent, decimal expectedTax, decimal expectedGross)
    {
        var (gross, tax) = TaxCalculator.SplitExclusive(net, ratePercent);

        Assert.Equal(expectedTax, tax);
        Assert.Equal(expectedGross, gross);
        Assert.Equal(gross, net + tax);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void SplitExclusive_NetPlusTax_AlwaysEqualsGross_AtAnyMinorUnitDigits(int minorUnitDigits)
    {
        decimal[] nets = [100m, 1000.555m, 12345.678m, 0.001m, 99.999m, 7m];

        foreach (var net in nets)
        {
            var (gross, tax) = TaxCalculator.SplitExclusive(net, 12.5m, minorUnitDigits);
            Assert.Equal(gross, net + tax);
        }
    }

    [Fact]
    public void SplitExclusive_ZeroNet_ReturnsZeroGrossAndZeroTax()
    {
        var (gross, tax) = TaxCalculator.SplitExclusive(0m, 10m);

        Assert.Equal(0m, gross);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void SplitExclusive_ZeroRate_ReturnsZeroTax_AndGrossEqualsNet()
    {
        var (gross, tax) = TaxCalculator.SplitExclusive(100m, 0m);

        Assert.Equal(0m, tax);
        Assert.Equal(100m, gross);
    }

    [Fact]
    public void SplitExclusive_RoundsMidpointAwayFromZero()
    {
        // 6.25 at 8%: 6.25 * 0.08 = 0.5 exactly at 0 minor digits -> rounds to 1 (away from zero)
        var (_, tax) = TaxCalculator.SplitExclusive(6.25m, 8m, 0);

        Assert.Equal(1m, tax);
    }

    [Fact]
    public void SplitExclusive_DefaultsToTwoMinorUnitDigits_WhenNotSpecified()
    {
        var withDefault = TaxCalculator.SplitExclusive(100m, 8m);
        var withExplicitTwo = TaxCalculator.SplitExclusive(100m, 8m, 2);

        Assert.Equal(withExplicitTwo, withDefault);
    }

    // --- issue #354: Split(mode) — the single dispatch point every posting service uses ---

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(3)]
    public void Split_Inclusive_MatchesSplitInclusive_AndGrossIsTheEnteredAmount(int minorUnitDigits)
    {
        const decimal entered = 110m;
        var (gross, net, tax) = TaxCalculator.Split(entered, TaxEntryMode.Inclusive, 10m, minorUnitDigits);
        var (expectedNet, expectedTax) = TaxCalculator.SplitInclusive(entered, 10m, minorUnitDigits);

        Assert.Equal(entered, gross);
        Assert.Equal(expectedNet, net);
        Assert.Equal(expectedTax, tax);
        Assert.Equal(gross, net + tax);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(3)]
    public void Split_Exclusive_MatchesSplitExclusive_AndNetIsTheEnteredAmount(int minorUnitDigits)
    {
        const decimal entered = 100m;
        var (gross, net, tax) = TaxCalculator.Split(entered, TaxEntryMode.Exclusive, 8m, minorUnitDigits);
        var (expectedGross, expectedTax) = TaxCalculator.SplitExclusive(entered, 8m, minorUnitDigits);

        Assert.Equal(entered, net);
        Assert.Equal(expectedGross, gross);
        Assert.Equal(expectedTax, tax);
        Assert.Equal(gross, net + tax);
    }

    [Fact]
    public void Split_Exclusive_AcceptanceFixture_100At8Percent_PostsNet100Tax8Gross108()
    {
        var (gross, net, tax) = TaxCalculator.Split(100m, TaxEntryMode.Exclusive, 8m);

        Assert.Equal(100m, net);
        Assert.Equal(8m, tax);
        Assert.Equal(108m, gross);
    }
}

using System.Globalization;
using StageFright.Core.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Tests for <see cref="MoneyInput.Parse"/> (spec 028, FR-007…FR-009). The raw value of an
/// <c>&lt;input type="number"&gt;</c> is always serialised by the browser with a period decimal
/// point and no digit grouping, no matter the device region, so it is parsed with
/// <see cref="CultureInfo.InvariantCulture"/> and never the active culture. Under fr-FR / de-DE
/// (comma decimal separator, period grouping) a value such as <c>"1.50"</c> must still parse to
/// <c>1.50m</c> — not <c>150</c> — and a null / blank / unparseable value falls back to <c>0m</c>.
/// </summary>
public sealed class MoneyInputTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _originalCulture;

    private static decimal ParseUnder(string cultureName, string? value)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        return MoneyInput.Parse(value);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("en-AU")]
    public void Should_ParseAPlainDecimalInvariantly_RegardlessOfCulture(string cultureName)
    {
        Assert.Equal(1.5m, ParseUnder(cultureName, "1.5"));
        Assert.Equal(1.50m, ParseUnder(cultureName, "1.50"));
        Assert.Equal(1000.5m, ParseUnder(cultureName, "1000.5"));
        Assert.Equal(-3.2m, ParseUnder(cultureName, "-3.2"));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_NotReadThePeriodAsAGroupSeparator_UnderACommaDecimalCulture(string cultureName)
    {
        // The bug this fixes: NumberStyles.Number + CurrentCulture reads "1.50" as 150 under fr-FR/de-DE.
        Assert.Equal(1.50m, ParseUnder(cultureName, "1.50"));
        Assert.NotEqual(150m, ParseUnder(cultureName, "1.50"));
        Assert.Equal(1000.5m, ParseUnder(cultureName, "1000.5"));
        Assert.NotEqual(10005m, ParseUnder(cultureName, "1000.5"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    public void Should_FallBackToZero_When_ValueIsNullBlankOrUnparseable(string? value)
    {
        Assert.Equal(0m, ParseUnder("fr-FR", value));
        Assert.Equal(0m, ParseUnder("de-DE", value));
        Assert.Equal(0m, ParseUnder("en-AU", value));
    }

    [Fact]
    public void Should_ProduceTheSameValueInEveryCulture_ForTheSameInput()
    {
        Assert.Equal(ParseUnder("en-AU", "1234.50"), ParseUnder("fr-FR", "1234.50"));
        Assert.Equal(ParseUnder("en-AU", "1234.50"), ParseUnder("de-DE", "1234.50"));
        Assert.Equal(1234.50m, ParseUnder("fr-FR", "1234.50"));
    }
}

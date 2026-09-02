using System.Globalization;
using StageFright.Core.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Tests for <see cref="MoneyFormatter"/> (spec 028, FR-003 / FR-004): the currency symbol,
/// ISO code and minor-unit precision come from the currency set via
/// <see cref="MoneyFormatter.Configure"/>, while digit grouping, the decimal separator and
/// symbol placement follow <see cref="CultureInfo.CurrentCulture"/>. A zero-decimal currency
/// (JPY) shows no fractional digits; the AUD default is byte-identical to the pre-028 output.
/// </summary>
public sealed class MoneyFormatterTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        MoneyFormatter.Configure(CurrencyCatalog.Default);
    }

    private static string FormatUnder(string cultureName, SupportedCurrency currency, decimal amount, bool withCode = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        MoneyFormatter.Configure(currency);
        return withCode ? MoneyFormatter.FormatWithCode(amount) : MoneyFormatter.Format(amount);
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_UseTheConfiguredSymbol_AndNeverTheActiveCultureCurrencySymbol(string cultureName)
    {
        var result = FormatUnder(cultureName, CurrencyCatalog.Get("AUD"), 1234.56m);

        Assert.Contains("$", result);
        Assert.DoesNotContain("€", result);   // fr-FR / de-DE own symbol must not leak in
        Assert.DoesNotContain("¤", result);   // the generic currency placeholder must be replaced
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_GroupDigitsPerActiveCulture_When_AmountExceedsOneThousand(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var expectedGrouping = "1" + culture.NumberFormat.CurrencyGroupSeparator + "234"; // "1,234" / "1.234" / "1 234"

        var result = FormatUnder(cultureName, CurrencyCatalog.Get("AUD"), 1234.56m);

        Assert.Contains(expectedGrouping, result);
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("fr-FR")]
    public void Should_ShowExactlyTwoFractionalDigits_ForATwoDecimalCurrency(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var result = FormatUnder(cultureName, CurrencyCatalog.Get("AUD"), 1234.5m);

        Assert.Contains(culture.NumberFormat.CurrencyDecimalSeparator + "50", result);
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Should_ShowNoFractionalDigits_ForAZeroDecimalCurrency(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var result = FormatUnder(cultureName, CurrencyCatalog.Get("JPY"), 1234.56m);

        Assert.Contains("¥", result);
        Assert.DoesNotContain(culture.NumberFormat.CurrencyDecimalSeparator + "5", result);
        // 1234.56 rounds to the whole unit 1235
        Assert.Contains("1" + culture.NumberFormat.CurrencyGroupSeparator + "235", result);
    }

    [Fact]
    public void Should_MatchThePreSpec028Output_ForAnAudAmount_UnderEnAu()
    {
        var result = FormatUnder("en-AU", CurrencyCatalog.Default, 1234.5m);

        Assert.Equal("$1,234.50", result);
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("en-US")]
    [InlineData("")]          // invariant culture — the default on CI hosts; would otherwise give "($1,234.50)"
    public void Should_RenderANegativeAmountWithALeadingMinus_NeverParentheses_ForAud(string cultureName)
    {
        var result = FormatUnder(cultureName, CurrencyCatalog.Default, -1234.5m);

        Assert.Equal("-$1,234.50", result);
    }

    [Fact]
    public void Should_KeepTheCultureSymbolPlacement_ForANegativeAmount_UnderAForeignCulture()
    {
        // fr-FR places the symbol after the amount; the negative form stays a leading minus, not "(…)".
        var result = FormatUnder("fr-FR", CurrencyCatalog.Get("AUD"), -1234.5m);

        Assert.StartsWith("-", result);
        Assert.EndsWith("$", result);
        Assert.DoesNotContain("(", result);
        Assert.DoesNotContain(")", result);
    }

    [Fact]
    public void Should_PrefixTheIsoCode_When_FormatWithCodeUsed()
    {
        Assert.Equal("AUD 1,234.50", FormatUnder("en-AU", CurrencyCatalog.Get("AUD"), 1234.5m, withCode: true));
        Assert.Equal("JPY 1,235", FormatUnder("en-AU", CurrencyCatalog.Get("JPY"), 1234.56m, withCode: true));
    }

    [Fact]
    public void Should_FallBackToAud_When_ConfiguredWithNull()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-AU");
        MoneyFormatter.Configure(null!);

        Assert.Equal("$5.00", MoneyFormatter.Format(5m));
    }
}

using StageFright.Core.Exceptions;
using StageFright.Core.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Tests for <see cref="CurrencyCatalog"/> (spec 028, FR-001): the curated ISO 4217 set spans
/// 0-, 2- and 3-minor-digit currencies, <see cref="CurrencyCatalog.Default"/> is the AUD "$"
/// entry, <see cref="CurrencyCatalog.TryGet"/> is a never-throwing case-insensitive lookup that
/// hands back <see cref="CurrencyCatalog.Default"/> on a miss, and
/// <see cref="CurrencyCatalog.Get"/> throws <see cref="ValidationException"/> for an unknown code.
/// </summary>
public sealed class CurrencyCatalogTests
{
    [Fact]
    public void Should_ExposeAudAsTheDefault_With_DollarSymbolAndTwoMinorDigits()
    {
        Assert.Equal("AUD", CurrencyCatalog.Default.Code);
        Assert.Equal("$", CurrencyCatalog.Default.Symbol);
        Assert.Equal(2, CurrencyCatalog.Default.MinorUnitDigits);
        Assert.Same(CurrencyCatalog.All[0], CurrencyCatalog.Default);
    }

    [Fact]
    public void Should_IncludeZeroAndThreeDecimalCurrencies_In_TheShippedSet()
    {
        Assert.Contains(CurrencyCatalog.All, c => c.Code == "JPY" && c.MinorUnitDigits == 0);
        Assert.Contains(CurrencyCatalog.All, c => c.Code == "KWD" && c.MinorUnitDigits == 3);
        Assert.Contains(CurrencyCatalog.All, c => c.Code == "BHD" && c.MinorUnitDigits == 3);
        Assert.All(CurrencyCatalog.All, c => Assert.Contains(c.MinorUnitDigits, new[] { 0, 2, 3 }));
    }

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("usd", 2)]
    [InlineData("  Eur  ", 2)]
    [InlineData("JPY", 0)]
    [InlineData("kwd", 3)]
    public void Should_ResolveCaseInsensitivelyAndTrimmed_When_TryGetCalledWithAKnownCode(string code, int expectedDigits)
    {
        var found = CurrencyCatalog.TryGet(code, out var currency);

        Assert.True(found);
        Assert.Equal(expectedDigits, currency.MinorUnitDigits);
        Assert.Equal(code.Trim().ToUpperInvariant(), currency.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ZZZ")]
    [InlineData("not-a-code")]
    public void Should_ReturnFalseAndHandBackDefault_When_TryGetCalledWithNullBlankOrUnknownCode(string? code)
    {
        var found = CurrencyCatalog.TryGet(code!, out var currency);

        Assert.False(found);
        Assert.Same(CurrencyCatalog.Default, currency);
    }

    [Theory]
    [InlineData("AUD")]
    [InlineData("aud")]
    [InlineData(" jpy ")]
    public void Should_ReturnTheMatchingEntry_When_GetCalledWithAKnownCode(string code)
    {
        var currency = CurrencyCatalog.Get(code);

        Assert.Equal(code.Trim().ToUpperInvariant(), currency.Code);
    }

    [Theory]
    [InlineData("ZZZ")]
    [InlineData("")]
    [InlineData("dollars")]
    public void Should_ThrowValidationException_When_GetCalledWithAnUnknownCode(string code)
    {
        Assert.Throws<ValidationException>(() => CurrencyCatalog.Get(code));
    }
}

using StageFright.Core.Modules.Settings;

namespace StageFright.Core.Tests.Abn;

/// <summary>Unit tests for AbnValidator's ATO weighted-modulus-89 checksum.</summary>
public class AbnValidatorTests
{
    // ATO's own published example ABN (used throughout their documentation and by other
    // implementations as a canonical valid test value).
    private const string KnownValidAbn = "51824753556";

    [Fact]
    public void IsValid_ReturnsTrue_ForKnownValidAbn()
    {
        Assert.True(AbnValidator.IsValid(KnownValidAbn));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenChecksumBroken()
    {
        // Flip the last digit so the checksum no longer divides by 89.
        Assert.False(AbnValidator.IsValid("51824753557"));
    }

    [Theory]
    [InlineData("5182475355")] // 10 digits
    [InlineData("518247535566")] // 12 digits
    [InlineData("")]
    public void IsValid_ReturnsFalse_ForWrongLength(string abn)
    {
        Assert.False(AbnValidator.IsValid(abn));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForNonDigitCharacters()
    {
        Assert.False(AbnValidator.IsValid("5182475355A"));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForNull()
    {
        Assert.False(AbnValidator.IsValid(null));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForAllZeros()
    {
        Assert.False(AbnValidator.IsValid("00000000000"));
    }
}

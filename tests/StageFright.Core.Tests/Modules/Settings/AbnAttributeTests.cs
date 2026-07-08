using StageFright.Core.Modules.Settings;

namespace StageFright.Core.Tests.Abn;

/// <summary>Unit tests for AbnAttribute: null/empty passes, valid ABN passes, malformed non-empty fails.</summary>
public class AbnAttributeTests
{
    private readonly AbnAttribute _attribute = new();

    [Fact]
    public void IsValid_ReturnsTrue_ForNull()
    {
        Assert.True(_attribute.IsValid(null));
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForEmptyString()
    {
        Assert.True(_attribute.IsValid(""));
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForValidAbn()
    {
        Assert.True(_attribute.IsValid("51824753556"));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForMalformedNonEmptyAbn()
    {
        Assert.False(_attribute.IsValid("12345"));
    }
}

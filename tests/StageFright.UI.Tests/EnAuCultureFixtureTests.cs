using System.Globalization;

namespace StageFright.UI.Tests;

/// <summary>The assembly fixture sets the process-wide culture; without it both defaults are null.</summary>
public class EnAuCultureFixtureTests
{
    [Fact]
    public void Should_SetProcessWideCultureToAustralianEnglish_When_UiTestsRun()
    {
        Assert.Equal("en-AU", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("en-AU", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.Equal("en-AU", CultureInfo.CurrentUICulture.Name);
    }
}

using System.Globalization;
using StageFright.Core.Modules.Settings;

namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Unit tests for <see cref="BackupFileNameBuilder"/> (spec 030, US2 / FR-010, SC-006): the default
/// name offered when creating a backup is <c>"&lt;sanitised org&gt; backup &lt;yyyy-MM-dd&gt;.sfbak"</c> —
/// the literal word <c>backup</c> is always present (Verbatim Constraint), invalid characters are
/// stripped, whitespace runs collapse, a blank organisation name falls back to a fixed constant,
/// and the date is always ISO <c>yyyy-MM-dd</c> regardless of the host's culture or calendar.
/// </summary>
public class BackupFileNameBuilderTests
{
    private static readonly DateOnly Sept8 = new(2026, 9, 8);

    [Fact]
    public void Build_ReturnsTheExactFormat_ForAPlainOrganisationName()
    {
        Assert.Equal("Cool Choir backup 2026-09-08.sfbak", BackupFileNameBuilder.Build("Cool Choir", Sept8));
    }

    [Theory]
    [InlineData("Cool Choir")]
    [InlineData("A")]
    [InlineData("St John's Singers")]
    public void Build_AlwaysContainsTheLiteralWordBackup(string org)
    {
        Assert.Contains(" backup ", BackupFileNameBuilder.Build(org, Sept8));
    }

    [Fact]
    public void Build_RemovesCharactersNotAllowedInAFileName()
    {
        // '/', ':', '*', '?', '"', '<', '>', '|' are all invalid on Windows.
        var name = BackupFileNameBuilder.Build("A/B:C*D?E\"F<G>H|I", Sept8);

        Assert.Equal("ABCDEFGHI backup 2026-09-08.sfbak", name);
        Assert.DoesNotContain(name[..name.LastIndexOf(".sfbak", StringComparison.Ordinal)], "/");
    }

    [Fact]
    public void Build_CollapsesWhitespaceRuns_AndTrims()
    {
        Assert.Equal("The Choir backup 2026-09-08.sfbak", BackupFileNameBuilder.Build("  The    Choir \t ", Sept8));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Build_FallsBackToTheFixedBaseName_WhenTheOrganisationNameIsBlank(string? org)
    {
        Assert.Equal("StageFright backup 2026-09-08.sfbak", BackupFileNameBuilder.Build(org, Sept8));
    }

    [Fact]
    public void Build_FallsBackToTheFixedBaseName_WhenTheOrganisationNameIsAllInvalidCharacters()
    {
        Assert.Equal($"{BackupFileNameBuilder.FallbackBaseName} backup 2026-09-08.sfbak",
            BackupFileNameBuilder.Build("//::**??", Sept8));
    }

    [Theory]
    [InlineData("fr-FR")]  // comma decimal separator, DD/MM date order
    [InlineData("de-DE")]
    [InlineData("th-TH")]  // Thai Buddhist calendar (year 2569, not 2026)
    [InlineData("ar-SA")]  // Umm al-Qura calendar
    public void Build_FormatsTheDateAsIsoYyyyMmDd_UnderAnyHostCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal("Choir backup 2026-09-08.sfbak", BackupFileNameBuilder.Build("Choir", Sept8));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Unit tests for <see cref="BackupSchema.IsRestorable"/> (spec 030, US2 / FR-019, SC-005): a file
/// recording a version equal to or older than <see cref="BackupSchema.CurrentSchemaVersion"/> on
/// every component is restorable; anything newer on any component — a newer major, minor, patch, or
/// a newer build of the same major version — is rejected, as is a null / empty / non-three-part /
/// non-numeric version string. Also covers <see cref="BackupSchema.IsOlderThan"/>, which decides
/// whether a restored file predates the spec 033 Petty Cash rename.
/// </summary>
public class BackupSchemaTests
{
    [Fact]
    public void CurrentSchemaVersion_IsThePublishedValue()
    {
        Assert.Equal("1.4.0", BackupSchema.CurrentSchemaVersion);
    }

    [Fact]
    public void Should_BeTheCurrentVersion_When_BankingReworkVersionRead()
    {
        Assert.Equal("1.4.0", BackupSchema.BankingReworkSchemaVersion);
    }

    [Theory]
    [InlineData("1.4.0")]   // equal
    [InlineData("1.3.0")]   // pre-banking file (spec 033, FR-022)
    [InlineData("1.2.0")]   // pre-organisations file (spec 032, FR-015)
    [InlineData("1.1.0")]   // older minor
    [InlineData("1.1.9")]   // older minor, newer patch within it
    [InlineData("1.0.0")]   // older minor
    [InlineData("0.9.9")]   // older major
    public void IsRestorable_ReturnsTrue_ForAnEqualOrOlderVersion(string version)
    {
        Assert.True(BackupSchema.IsRestorable(version));
    }

    [Theory]
    [InlineData("2.0.0")]   // newer major
    [InlineData("1.5.0")]   // newer minor
    [InlineData("1.4.1")]   // newer patch
    [InlineData("1.4.99")]  // newer build of the same major.minor
    [InlineData("10.0.0")]
    public void IsRestorable_ReturnsFalse_ForAnyVersionNewerOnAnyComponent(string version)
    {
        Assert.False(BackupSchema.IsRestorable(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.0.0")]
    [InlineData("1.x.0")]
    [InlineData("v1.2.0")]
    [InlineData("1.2.0-beta")]
    [InlineData("abc")]
    [InlineData("1. 2. 0")]
    public void IsRestorable_ReturnsFalse_ForANullOrUnparseableVersion(string? version)
    {
        Assert.False(BackupSchema.IsRestorable(version));
    }

    [Theory]
    [InlineData("1.3.0")]
    [InlineData("1.2.0")]
    [InlineData("1.3.99")]
    public void Should_ReturnTrue_When_FileVersionIsOlderThan140(string version)
    {
        Assert.True(BackupSchema.IsOlderThan(version, "1.4.0"));
    }

    [Theory]
    [InlineData("1.4.0")]   // equal is not older
    [InlineData("1.4.1")]
    [InlineData("1.5.0")]
    [InlineData("2.0.0")]
    public void Should_ReturnFalse_When_FileVersionIsEqualOrNewer(string version)
    {
        Assert.False(BackupSchema.IsOlderThan(version, "1.4.0"));
    }

    [Theory]
    [InlineData("0.9.9", "1.0.0")]   // older major
    [InlineData("1.3.9", "1.4.0")]   // older minor
    [InlineData("1.4.0", "1.4.1")]   // older patch
    public void Should_CompareEveryComponent_When_CheckingIsOlderThan(string fileVersion, string version)
    {
        Assert.True(BackupSchema.IsOlderThan(fileVersion, version));
        Assert.False(BackupSchema.IsOlderThan(version, fileVersion));
    }

    [Theory]
    [InlineData(null, "1.4.0")]
    [InlineData("", "1.4.0")]
    [InlineData("abc", "1.4.0")]
    [InlineData("1.3.0", "not-a-version")]
    public void Should_ReturnFalse_When_EitherVersionIsUnparseable(string? fileVersion, string version)
    {
        Assert.False(BackupSchema.IsOlderThan(fileVersion, version));
    }
}

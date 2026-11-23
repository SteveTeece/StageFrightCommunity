namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Unit tests for <see cref="BackupSchema.IsRestorable"/> (spec 030, US2 / FR-019, SC-005): a file
/// recording a version equal to or older than <see cref="BackupSchema.CurrentSchemaVersion"/> on
/// every component is restorable; anything newer on any component — a newer major, minor, patch, or
/// a newer build of the same major version — is rejected, as is a null / empty / non-three-part /
/// non-numeric version string.
/// </summary>
public class BackupSchemaTests
{
    [Fact]
    public void CurrentSchemaVersion_IsThePublishedValue()
    {
        Assert.Equal("1.2.0", BackupSchema.CurrentSchemaVersion);
    }

    [Theory]
    [InlineData("1.2.0")]   // equal
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
    [InlineData("1.3.0")]   // newer minor
    [InlineData("1.2.1")]   // newer patch
    [InlineData("1.2.99")]  // newer build of the same major.minor
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
}

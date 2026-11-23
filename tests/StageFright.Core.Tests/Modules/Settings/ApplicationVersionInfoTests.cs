using StageFright.Core.Modules.Settings.Backup;

namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Unit tests for <see cref="ApplicationVersionInfo"/> (spec 030 FR-003): the originating
/// application version stamped into a backup reflects the build, with the SDK's full commit
/// SHA build metadata shortened to seven characters.
/// </summary>
public class ApplicationVersionInfoTests
{
    [Fact]
    public void Should_ShortenCommitSha_When_BuildMetadataIsFullSha()
    {
        Assert.Equal("1.0.0+20472ad", ApplicationVersionInfo.Format("1.0.0+20472ad219fa2ab21d6a1cabe61d59c6e33f3541"));
    }

    [Theory]
    [InlineData("1.0.0")]          // no build metadata
    [InlineData("1.0.0+20472ad")]  // metadata already seven characters
    [InlineData("1.0.0+abc")]      // metadata shorter than seven characters
    [InlineData("1.0.0+")]         // empty metadata
    public void Should_ReturnUnchanged_When_BuildMetadataNotLongerThanShortSha(string version)
    {
        Assert.Equal(version, ApplicationVersionInfo.Format(version));
    }

    [Fact]
    public void Should_ReflectCoreAssemblyVersion_When_CurrentRead()
    {
        var assemblyVersion = typeof(ApplicationVersionInfo).Assembly.GetName().Version!;

        Assert.StartsWith($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", ApplicationVersionInfo.Current);
    }
}

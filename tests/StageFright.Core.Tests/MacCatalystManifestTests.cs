using System.Xml.Linq;

namespace StageFright.Core.Tests;

/// <summary>
/// The Mac build is universal (x64 + arm64, debug-pre-release.yml), so its Info.plist must not require arm64,
/// or the Intel slice would refuse to launch on an Intel Mac.
/// </summary>
public class MacCatalystManifestTests
{
    [Fact]
    public void Should_NotRequireArm64_When_MacCatalystInfoPlistRead()
    {
        var plist = XDocument.Load(Path.Combine(RepoRoot(), "src", "StageFright.App", "Platforms", "MacCatalyst", "Info.plist"));
        var keys = plist.Root!.Element("dict")!.Elements("key").Select(k => k.Value).ToList();

        Assert.NotEmpty(keys);
        Assert.DoesNotContain("UIRequiredDeviceCapabilities", keys);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StageFrightCommunity.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate StageFrightCommunity.slnx above " + AppContext.BaseDirectory);
    }
}

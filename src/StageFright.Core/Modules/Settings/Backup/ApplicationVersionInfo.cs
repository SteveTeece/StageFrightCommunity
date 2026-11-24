using System.Reflection;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// The version of the build that is running, as stamped into a backup's
/// <see cref="BackupEnvelope.ApplicationVersion"/> (spec 030 FR-003: it must reflect the build
/// that produced the file, never a fixed placeholder). Display-only — never a compatibility axis.
/// </summary>
public static class ApplicationVersionInfo
{
    private const int ShortCommitLength = 7;

    /// <summary>
    /// The running build's version, from the assembly's informational version
    /// (e.g. <c>1.0.0+20472ad</c>), falling back to the assembly version.
    /// </summary>
    public static string Current { get; } = Format(
        typeof(ApplicationVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ApplicationVersionInfo).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// Shortens the SDK's full commit SHA build metadata (<c>1.0.0+&lt;40 hex chars&gt;</c>)
    /// to the first seven characters, so the version stays readable in the restore summary.
    /// A version without build metadata is returned unchanged.
    /// </summary>
    public static string Format(string version)
    {
        var plus = version.IndexOf('+');
        var metadataLength = plus < 0 ? 0 : version.Length - plus - 1;
        return metadataLength > ShortCommitLength
            ? version[..(plus + 1 + ShortCommitLength)]
            : version;
    }
}

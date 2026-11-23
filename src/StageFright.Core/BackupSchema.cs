using System.Globalization;

namespace StageFright.Core;

/// <summary>
/// The <c>.sfbak</c> backup-file schema version this build writes and understands, and the rule
/// for whether a given file's recorded version can be restored (FR-019).
/// </summary>
public static class BackupSchema
{
    /// <summary>The backup-file schema version produced by this build (a three-part semantic version).</summary>
    public const string CurrentSchemaVersion = "1.2.0";

    /// <summary>
    /// True when a file recording <paramref name="fileVersion"/> can be restored into this build:
    /// the value parses as a three-part numeric semantic version and is equal to or older than
    /// <see cref="CurrentSchemaVersion"/> on every component. False when it is null, empty, not a
    /// three-part numeric version, or newer than this build on any component — including a newer
    /// build of the <em>same</em> major version, which may carry settings or record types this
    /// build does not recognise. An older or equal file is accepted; EF Core startup migration
    /// brings an older restored database forward.
    /// </summary>
    public static bool IsRestorable(string? fileVersion)
    {
        if (!TryParse(fileVersion, out var file))
            return false;

        var current = ParseCurrent();

        if (file.Major != current.Major)
            return file.Major < current.Major;
        if (file.Minor != current.Minor)
            return file.Minor < current.Minor;
        return file.Patch <= current.Patch;
    }

    private static (int Major, int Minor, int Patch) ParseCurrent() =>
        TryParse(CurrentSchemaVersion, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"BackupSchema.CurrentSchemaVersion ('{CurrentSchemaVersion}') is not a three-part numeric version.");

    private static bool TryParse(string? version, out (int Major, int Minor, int Patch) parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(version))
            return false;

        var parts = version.Split('.');
        if (parts.Length != 3)
            return false;

        if (!TryParseComponent(parts[0], out var major) ||
            !TryParseComponent(parts[1], out var minor) ||
            !TryParseComponent(parts[2], out var patch))
            return false;

        parsed = (major, minor, patch);
        return true;
    }

    private static bool TryParseComponent(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

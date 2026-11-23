using System.Globalization;
using System.Text.RegularExpressions;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Builds the default file name offered when creating a backup:
/// <c>"&lt;sanitised org&gt; backup &lt;yyyy-MM-dd&gt;.sfbak"</c> (FR-010). Pure and host-locale
/// independent — the date always uses the invariant culture and the literal word <c>backup</c> is
/// always present.
/// </summary>
public static partial class BackupFileNameBuilder
{
    /// <summary>Base name used when the organisation name is blank, whitespace, or all invalid characters.</summary>
    public const string FallbackBaseName = "StageFright";

    private const string Extension = ".sfbak";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    /// <summary>
    /// Returns <c>"&lt;sanitised org&gt; backup &lt;date:yyyy-MM-dd&gt;.sfbak"</c>. Invalid file-name
    /// characters are removed, whitespace runs collapse to a single space and are trimmed, and a
    /// blank/whitespace organisation name yields <c>"StageFright backup &lt;date&gt;.sfbak"</c>.
    /// </summary>
    public static string Build(string? organisationName, DateOnly date)
    {
        var baseName = Sanitise(organisationName);
        if (baseName.Length == 0)
            baseName = FallbackBaseName;

        var datePart = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{baseName} backup {datePart}{Extension}";
    }

    private static string Sanitise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var stripped = new string(value.Where(c => Array.IndexOf(invalid, c) < 0).ToArray());
        return WhitespaceRun().Replace(stripped, " ").Trim();
    }
}

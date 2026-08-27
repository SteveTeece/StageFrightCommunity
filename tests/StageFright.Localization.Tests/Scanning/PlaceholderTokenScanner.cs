using System.Text.RegularExpressions;

namespace StageFright.Localization.Tests.Scanning;

/// <summary>
/// Extracts named placeholder tokens (e.g. "{OrganisationName}", "{Count}") from a resource
/// value string. Used by the placeholder-parity guard (Phase 3+) to compare the token set
/// between a neutral entry and its satellite translation.
/// </summary>
public static class PlaceholderTokenScanner
{
    private static readonly Regex TokenPattern = new(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.Compiled);

    public static IReadOnlySet<string> ScanValue(string value) =>
        TokenPattern.Matches(value).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
}

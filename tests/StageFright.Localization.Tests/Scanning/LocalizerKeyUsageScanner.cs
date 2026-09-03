using System.Text.RegularExpressions;

namespace StageFright.Localization.Tests.Scanning;

/// <summary>
/// Extracts resource-key usages from .razor / .razor.cs / .cs source text: the
/// <c>IStringLocalizer</c> indexer (<c>L["Key"]</c>, <c>L["Key", args]</c>),
/// <c>ILocalizer.Get&lt;T&gt;("Key")</c>, and <c>ILocalizer.Plural&lt;T&gt;("Key", ...)</c>.
/// Infrastructure only — the guard tests written from Phase 3 onward assert against its output;
/// this class makes no assertions itself.
/// </summary>
public static class LocalizerKeyUsageScanner
{
    private static readonly Regex IndexerPattern = new(
        @"[A-Za-z_][A-Za-z0-9_]*\[\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex GetOrPluralPattern = new(
        @"\.(?:Get|Plural)<[^>]+>\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    public static IReadOnlyList<LocalizerKeyUsage> ScanFile(string filePath)
    {
        var usages = new List<LocalizerKeyUsage>();
        var lines = File.ReadAllLines(filePath);

        for (var i = 0; i < lines.Length; i++)
        {
            AddMatches(usages, IndexerPattern, lines[i], filePath, i + 1);
            AddMatches(usages, GetOrPluralPattern, lines[i], filePath, i + 1);
        }

        return usages;
    }

    public static IReadOnlyList<LocalizerKeyUsage> ScanDirectory(string directoryPath, params string[] searchPatterns)
    {
        var patterns = searchPatterns.Length > 0 ? searchPatterns : new[] { "*.razor", "*.razor.cs", "*.cs" };
        var usages = new List<LocalizerKeyUsage>();

        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, pattern, SearchOption.AllDirectories))
            {
                usages.AddRange(ScanFile(file));
            }
        }

        return usages;
    }

    private static void AddMatches(List<LocalizerKeyUsage> usages, Regex pattern, string line, string filePath, int lineNumber)
    {
        foreach (Match match in pattern.Matches(line))
        {
            usages.Add(new LocalizerKeyUsage(match.Groups[1].Value, filePath, lineNumber));
        }
    }
}

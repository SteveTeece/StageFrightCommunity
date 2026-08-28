using System.Text.RegularExpressions;
using StageFright.Localization.Tests.Scanning;

namespace StageFright.Localization.Tests;

/// <summary>
/// Localization guard suite scoped to the Rehearsals module slice (US2, T032). Enforces the
/// resource-key-catalog.md §3 contract for that slice: baseline completeness, no residual
/// user-facing literals (incl. <c>aria-label</c>/<c>alt</c>/<c>title</c>/<c>placeholder</c> —
/// FR-001), and no <c>"C"</c> currency format (FR-015 — use <c>MoneyFormatter</c>). The
/// missing-key warning/fallback path and repo-wide unscoping are covered by
/// <see cref="Us1LocalizationGuardTests"/> and the still-pending T029/T060.
/// </summary>
public class RehearsalsLocalizationGuardTests
{
    private static readonly string[] RehearsalsRelativeFiles =
    [
        "src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor",
        "src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor.cs",
        "src/StageFright.UI/Pages/Rehearsals/RehearsalForm.razor",
        "src/StageFright.UI/Pages/Rehearsals/RehearsalForm.razor.cs",
        "src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor",
        "src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor.cs",
    ];

    private static readonly Dictionary<string, string> AreaResx = new()
    {
        ["Rehearsals"] = "src/StageFright.UI/Resources/Strings/RehearsalsResource.resx",
        ["Shared"] = "src/StageFright.UI/Resources/Strings/SharedResource.resx",
    };

    [Fact]
    public void Should_HaveNeutralEntry_When_KeyReferencedInRehearsals()
    {
        var missing = new List<string>();

        foreach (var relative in RehearsalsRelativeFiles)
        {
            var path = RepoPath(relative);
            foreach (var usage in LocalizerKeyUsageScanner.ScanFile(path))
            {
                var area = usage.Key.Split('_', 2)[0];
                if (!AreaResx.TryGetValue(area, out var resxRelative))
                    continue; // not an area-prefixed localization key (defensive)

                var neutral = ResxKeyScanner.ScanFile(RepoPath(resxRelative));

                var present = neutral.ContainsKey(usage.Key)
                    || (neutral.ContainsKey(usage.Key + "_One") && neutral.ContainsKey(usage.Key + "_Other"));

                if (!present)
                    missing.Add($"{usage.Key}  ({relative}:{usage.LineNumber})  -> {resxRelative}");
            }
        }

        Assert.True(missing.Count == 0,
            "Rehearsals slice references localization keys with no neutral .resx entry:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Should_HaveNoUserFacingLiteral_When_RehearsalsFileScanned()
    {
        var razorText = new Regex(@">\s*([A-Za-z][A-Za-z ,.'!?()\-]{2,})\s*<", RegexOptions.Compiled);
        var attrLiteral = new Regex(
            @"\b(aria-label|alt|title|placeholder)\s*=\s*""([^""@]*[A-Za-z]{3,}[^""]*)""",
            RegexOptions.Compiled);
        var csPhrase = new Regex(@"""([A-Z][a-z]+(?: [A-Za-z]+)+[.!?]?)""", RegexOptions.Compiled);

        var findings = new List<string>();

        foreach (var relative in RehearsalsRelativeFiles)
        {
            var lines = File.ReadAllLines(RepoPath(relative));
            var isRazor = relative.EndsWith(".razor", StringComparison.Ordinal);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (isRazor)
                {
                    foreach (Match m in razorText.Matches(line))
                    {
                        var text = m.Groups[1].Value.Trim();
                        if (IsAllowedToken(text)) continue;
                        findings.Add($"{relative}:{i + 1}  text node \"{text}\"");
                    }

                    foreach (Match m in attrLiteral.Matches(line))
                        findings.Add($"{relative}:{i + 1}  {m.Groups[1].Value}=\"{m.Groups[2].Value}\"");
                }
                else
                {
                    if (line.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue; // doc comment
                    if (line.Contains("Logger.Log", StringComparison.Ordinal)) continue;         // diagnostic log text may stay English (FR-007)
                    foreach (Match m in csPhrase.Matches(line))
                    {
                        var text = m.Groups[1].Value;
                        if (IsAllowedToken(text)) continue;
                        findings.Add($"{relative}:{i + 1}  string literal \"{text}\"");
                    }
                }
            }
        }

        Assert.True(findings.Count == 0,
            "Rehearsals slice still contains user-facing string literals (should come from resources):\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_NotUseCFormat_When_RehearsalsFileScanned()
    {
        var cFormat = new Regex(
            @"ToString\(\s*""C\d?""\s*\)|""\{0:C\d?\}""|FormatString\s*=\s*""\{0:C",
            RegexOptions.Compiled);

        var findings = new List<string>();
        foreach (var relative in RehearsalsRelativeFiles)
        {
            var lines = File.ReadAllLines(RepoPath(relative));
            for (var i = 0; i < lines.Length; i++)
                if (cFormat.IsMatch(lines[i]))
                    findings.Add($"{relative}:{i + 1}  {lines[i].Trim()}");
        }

        Assert.True(findings.Count == 0,
            "Rehearsals slice formats a monetary amount with the culture currency symbol (\"C\"); use MoneyFormatter:\n"
            + string.Join("\n", findings));
    }

    private static bool IsAllowedToken(string text) => !text.Any(char.IsLetter);

    private static string RepoPath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StageFrightCommunity.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate StageFrightCommunity.slnx above " + AppContext.BaseDirectory);
    }
}

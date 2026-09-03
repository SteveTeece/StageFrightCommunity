using System.Text.RegularExpressions;

namespace StageFright.Localization.Tests;

/// <summary>
/// Guard for spec 028 US2 (FR-008 / FR-009): every money-entry field must interpret a typed
/// amount the same way. The value of an <c>&lt;input type="number"&gt;</c> is serialised
/// invariant by the browser, so it must be parsed with
/// <see cref="System.Globalization.CultureInfo.InvariantCulture"/> through the shared
/// <c>MoneyInput.Parse</c> helper — never <c>decimal.TryParse(…, CultureInfo.CurrentCulture)</c>,
/// which reads the period as a thousands separator under fr-FR / de-DE and scales the amount.
/// This complements <c>Us2LocalizationGuardTests</c>, which guards money <em>formatting</em>
/// ("C") rather than money <em>input parsing</em>.
/// </summary>
public class MoneyInputGuardTests
{
    /// <summary>
    /// <c>decimal.TryParse(…</c> / <c>decimal.Parse(…</c> whose argument list (up to the next
    /// statement terminator) names <c>CultureInfo.CurrentCulture</c> / <c>CurrentUICulture</c>.
    /// </summary>
    private static readonly Regex CurrentCultureParse = new(
        @"(?:decimal|Decimal)\.(?:TryParse|Parse)\([^;]*?CultureInfo\.Current(?:UI)?Culture",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>The hand-rolled money-entry handlers that must route typed amounts through the shared helper.</summary>
    private static readonly string[] MoneyEntryHandlers =
    [
        "src/StageFright.UI/Pages/Finance/JournalEntryPage.razor.cs",
        "src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor.cs",
    ];

    [Fact]
    public void Should_NotParseAnyInputWithCurrentCulture_When_UiSourceScanned()
    {
        var root = RepoRoot();
        var uiRoot = Path.Combine(root, "src", "StageFright.UI");
        var findings = new List<string>();

        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildArtifact(file)) continue;

            foreach (Match m in CurrentCultureParse.Matches(File.ReadAllText(file)))
                findings.Add($"{Path.GetRelativePath(root, file)}  {Collapse(m.Value)}");
        }

        Assert.True(findings.Count == 0,
            "A money/number field parses input with CultureInfo.CurrentCulture — route it through "
            + "MoneyInput.Parse (invariant) instead:\n" + string.Join("\n", findings));
    }

    [Fact]
    public void Should_RouteEveryHandRolledMoneyEntry_ThroughMoneyInputParse()
    {
        var missing = MoneyEntryHandlers
            .Where(rel => !File.ReadAllText(RepoPath(rel)).Contains("MoneyInput.Parse", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            "These hand-rolled money-entry handlers no longer call MoneyInput.Parse:\n"
            + string.Join("\n", missing));
    }

    // --- Helpers ---------------------------------------------------------------

    private static bool IsBuildArtifact(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string Collapse(string s) => Regex.Replace(s, @"\s+", " ").Trim();

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

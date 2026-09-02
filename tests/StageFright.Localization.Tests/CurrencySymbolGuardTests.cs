using System.Text.RegularExpressions;

namespace StageFright.Localization.Tests;

/// <summary>
/// Spec 028 US1 guard (FR-004 / SC-002): no <c>StageFright.Reports</c> report provider and no
/// <c>StageFright.UI</c> money-display site may bake in a currency presentation. Concretely, the
/// scanned surface must never
/// <list type="bullet">
///   <item>format a monetary value with the culture-currency specifier — <c>ToString("C")</c> /
///     <c>"{0:C}"</c> / <c>FormatString="{0:C}"</c> / an interpolated <c>:C</c> hole — which
///     substitutes the <em>active culture's</em> symbol (e.g. <c>€</c> under <c>fr-FR</c>) and
///     misrepresents the amount;</item>
///   <item>format a monetary value with a fixed two-decimal specifier — <c>ToString("F2")</c> /
///     an interpolated <c>:F2</c> hole — which is wrong for a 0- or 3-minor-digit currency
///     (yen, dinar);</item>
///   <item>emit a hard-coded <c>"$"</c> symbol or <c>"AUD"</c> code string literal next to a
///     value.</item>
/// </list>
/// Every displayed, printed, or exported amount must route through
/// <see cref="StageFright.Core.Localization.MoneyFormatter"/>, which reads the organisation's
/// configured currency (<c>Settings.CurrencyCode</c>) for symbol / code / minor-unit precision
/// and lets only grouping and placement follow <c>CultureInfo.CurrentCulture</c>.
///
/// This complements <c>Us2LocalizationGuardTests.Should_NotUseCFormat_When_AnyDisplaySiteScanned</c>
/// (the repo-wide <c>"C"</c>-format sweep) by adding the <c>"$"</c> / <c>"AUD"</c> literal and
/// <c>"F2"</c> checks, and <c>MoneyInputGuardTests</c> (money <em>input parsing</em>) by asserting
/// the money-bearing providers still <em>format</em> through the shared helper.
/// </summary>
public class CurrencySymbolGuardTests
{
    /// <summary>The report-provider + UI surface whose amounts must render via <c>MoneyFormatter</c>.</summary>
    private static readonly string[] ScanDirectories =
    [
        "src/StageFright.Reports/Providers",
        "src/StageFright.UI",
    ];

    /// <summary>
    /// A monetary <c>ToString</c> / interpolation format that hard-codes presentation: the
    /// culture-currency specifier <c>C</c>(digits) in any of its forms, or the fixed
    /// two-decimal <c>F2</c> specifier.
    /// </summary>
    private static readonly Regex BakedMoneyFormat = new(
        """
        ToString\(\s*"C\d?"\s*[,)]|"\{0:C\d?\}"|FormatString\s*=\s*"\{0:C|:C\d?\}"|ToString\(\s*"F2"\s*[,)]|:F2\}"
        """,
        RegexOptions.Compiled);

    /// <summary>
    /// A string literal whose visible content <em>starts</em> with a bare <c>$</c> (e.g.
    /// <c>"$"</c>, <c>"$ "</c>, <c>"${amount}"</c>, <c>"$0.00"</c>) or an interpolated string
    /// that opens with a literal <c>$</c> before its first hole (<c>$"${amount:N2}"</c>).
    /// </summary>
    private static readonly Regex HardDollarLiteral = new(
        """
        "\$[ \t]*(?:\{|\d|")|\$"\$\{
        """,
        RegexOptions.Compiled);

    /// <summary>A string literal that begins with the ISO code <c>AUD</c> (<c>"AUD"</c>, <c>"AUD "</c>, <c>"AUD 1,234.50"</c>).</summary>
    private static readonly Regex HardAudLiteral = new(
        """
        "AUD\b
        """,
        RegexOptions.Compiled);

    /// <summary>
    /// Report providers that carry money columns and therefore must reference
    /// <c>MoneyFormatter</c> (the two omitted providers — <c>CommitteeReportProvider</c>,
    /// <c>MemberListReportProvider</c> — render no monetary value).
    /// </summary>
    private static readonly string[] MoneyBearingProviders =
    [
        "AccountRegisterReportProvider.cs",
        "BalanceSheetReportProvider.cs",
        "BankReconciliationReportProvider.cs",
        "ChartOfAccountsReportProvider.cs",
        "GeneralLedgerReportProvider.cs",
        "IncomeStatementReportProvider.cs",
        "MemberAccountSummaryReportProvider.cs",
        "TaxSummaryReportProvider.cs",
        "TrialBalanceReportProvider.cs",
    ];

    [Fact]
    public void Should_NotBakeInAMoneyFormat_When_ReportAndUiSurfaceScanned()
    {
        var findings = ScanForLineMatches(BakedMoneyFormat, static (line, _) =>
            !line.Contains("Regex", StringComparison.Ordinal)          // regex pattern / backreference, not a format
            && !line.Contains(".Replace(", StringComparison.Ordinal));

        Assert.True(findings.Count == 0,
            "A monetary value is formatted with the culture-currency (\"C\") or fixed-two-decimal (\"F2\") "
            + "specifier — route it through MoneyFormatter.Format / FormatWithCode:\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_NotEmitAHardCodedCurrencySymbolOrCode_When_ReportAndUiSurfaceScanned()
    {
        bool NotAConfiguredCurrencyIdentity(string line, string _) =>
            // `settings?.CurrencyCode ?? "AUD"`, `CurrencyCode { get; set; } = "AUD"` — the ISO
            // 4217 configuration default sanctioned by FR-001, not a display literal.
            !line.Contains("CurrencyCode", StringComparison.Ordinal)
            && !line.Contains("CurrencyCatalog", StringComparison.Ordinal);

        var findings = ScanForLineMatches(HardDollarLiteral, static (_, _) => true);
        findings.AddRange(ScanForLineMatches(HardAudLiteral, NotAConfiguredCurrencyIdentity));

        Assert.True(findings.Count == 0,
            "A money-display site hard-codes a \"$\" symbol or \"AUD\" code literal — the symbol and "
            + "code must come from MoneyFormatter (the configured Settings.CurrencyCode):\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_RouteMoneyThroughMoneyFormatter_When_MoneyBearingProviderScanned()
    {
        var providersDir = RepoPath("src/StageFright.Reports/Providers");

        var missing = MoneyBearingProviders
            .Where(name => !File.ReadAllText(Path.Combine(providersDir, name))
                .Contains("MoneyFormatter", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            "These money-bearing report providers no longer reference MoneyFormatter:\n"
            + string.Join("\n", missing));
    }

    // --- Helpers ---------------------------------------------------------------

    /// <summary>
    /// Runs <paramref name="pattern"/> against every non-comment line of every <c>.cs</c> /
    /// <c>.razor</c> file under <see cref="ScanDirectories"/>, keeping a match only when
    /// <paramref name="keep"/> (given the raw line and the repo-relative path) allows it.
    /// </summary>
    private static List<string> ScanForLineMatches(Regex pattern, Func<string, string, bool> keep)
    {
        var root = RepoRoot();
        var findings = new List<string>();

        foreach (var dir in ScanDirectories)
        {
            var abs = Path.Combine(root, dir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(abs)) continue;

            foreach (var file in Directory.EnumerateFiles(abs, "*.*", SearchOption.AllDirectories))
            {
                if (IsBuildArtifact(file)) continue;
                if (!file.EndsWith(".cs", StringComparison.Ordinal)
                    && !file.EndsWith(".razor", StringComparison.Ordinal)) continue;
                if (Path.GetFileName(file) == "MoneyFormatter.cs") continue; // the one sanctioned "C" implementation

                var rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("///", StringComparison.Ordinal)
                        || trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.Contains("nameof(", StringComparison.Ordinal)) continue;
                    if (line.Contains("Logger.Log", StringComparison.Ordinal)
                        || line.Contains(".Log(", StringComparison.Ordinal)) continue; // diagnostic text (FR-007 exempt)

                    if (pattern.IsMatch(line) && keep(line, rel))
                        findings.Add($"{rel}:{i + 1}  {line.Trim()}");
                }
            }
        }

        return findings;
    }

    private static bool IsBuildArtifact(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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

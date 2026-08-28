using System.Text.RegularExpressions;
using StageFright.Core.Enums;
using StageFright.Localization.Tests.Scanning;

namespace StageFright.Localization.Tests;

/// <summary>
/// Repo-wide localization guard for US2 (T029). Where <see cref="Us1LocalizationGuardTests"/>
/// and the per-module interim guards each cover one slice, this suite enforces the
/// resource-key-catalog.md §3 contract across the whole application surface converted by US1 +
/// US2 — every screen, tile, menu, report provider/renderer and shared component:
/// <list type="bullet">
///   <item>baseline completeness — every referenced localization key has a neutral (en-AU) entry (SC-008);</item>
///   <item>enum coverage — <c>EnumsResource</c> has an <c>Enum_&lt;Type&gt;_&lt;Member&gt;</c> entry for every member of each user-facing enum, and no screen renders one raw (FR-024);</item>
///   <item>no orphan satellite keys — every key in a shipped <c>.&lt;culture&gt;.resx</c> also exists in its neutral file;</item>
///   <item>placeholder parity — a plural <c>_One</c>/<c>_Other</c> pair uses the same named tokens, and every plural half has its partner (FR-010);</item>
///   <item>no <c>"C"</c> currency format at any display site repo-wide — use <c>MoneyFormatter</c> (FR-015).</item>
/// </list>
/// The final removal of per-phase scoping for a strict zero-literal assertion is T060 (Polish).
/// </summary>
public class Us2LocalizationGuardTests
{
    // --- Surface definition -------------------------------------------------------

    /// <summary>Directories whose .razor / .razor.cs / .cs are user-facing UI converted by US1 + US2.</summary>
    private static readonly string[] Us2ScanDirectories =
    [
        "src/StageFright.UI/Layout",
        "src/StageFright.UI/Pages",
        "src/StageFright.UI/Modules",
        "src/StageFright.UI/Shared",
        "src/StageFright.Reports/Providers",
        "src/StageFright.Reports/Rendering",
    ];

    /// <summary>
    /// The subset of <see cref="Us2ScanDirectories"/> scanned for residual UI literals. Report
    /// providers/renderers are excluded: their user-facing text is guarded by
    /// <c>StageFright.Reports.Tests/ReportsResourceLabelTests</c> (T030), and what remains as
    /// literals there is exclusively culture-invariant filter option-value tokens (T039 —
    /// the localised label lives in the parallel <c>OptionLabels</c>).
    /// </summary>
    private static readonly string[] ResidualLiteralDirectories =
    [
        "src/StageFright.UI/Layout",
        "src/StageFright.UI/Pages",
        "src/StageFright.UI/Modules",
        "src/StageFright.UI/Shared",
    ];

    /// <summary>The area key-prefix → owning neutral .resx map (resource-key-catalog.md §1).</summary>
    private static readonly Dictionary<string, string> AreaResx = new(StringComparer.Ordinal)
    {
        ["Nav"] = "src/StageFright.Core/Modules/Localization/Resources/NavigationResource.resx",
        ["Validation"] = "src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx",
        ["Enum"] = "src/StageFright.Core/Modules/Localization/Resources/EnumsResource.resx",
        ["Reports"] = "src/StageFright.Reports/Resources/ReportsResource.resx",
        ["Shared"] = "src/StageFright.UI/Resources/Strings/SharedResource.resx",
        ["Dashboard"] = "src/StageFright.UI/Resources/Strings/DashboardResource.resx",
        ["Members"] = "src/StageFright.UI/Resources/Strings/MembersResource.resx",
        ["Rehearsals"] = "src/StageFright.UI/Resources/Strings/RehearsalsResource.resx",
        ["Events"] = "src/StageFright.UI/Resources/Strings/EventsResource.resx",
        ["Finance"] = "src/StageFright.UI/Resources/Strings/FinanceResource.resx",
        ["Settings"] = "src/StageFright.UI/Resources/Strings/SettingsResource.resx",
        ["Setup"] = "src/StageFright.UI/Resources/Strings/SetupResource.resx",
    };

    /// <summary>Every enum whose values are shown to a user and must resolve via <c>EnumsResource</c> (FR-024).</summary>
    private static readonly Type[] UserFacingEnums =
    [
        typeof(MemberStatus), typeof(Theme), typeof(FeeType), typeof(PaymentMethod),
        typeof(PaymentType), typeof(AccountType), typeof(TaxCode),
        typeof(ReconciliationStatus), typeof(JournalEntryType),
    ];

    // --- Baseline completeness --------------------------------------------------------

    [Fact]
    public void Should_HaveNeutralEntry_When_KeyReferencedAnywhereInApp()
    {
        var resxCache = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var file in EnumerateSourceFiles(Us2ScanDirectories))
        {
            foreach (var usage in LocalizerKeyUsageScanner.ScanFile(file))
            {
                var area = usage.Key.Split('_', 2)[0];
                if (!AreaResx.TryGetValue(area, out var resxRelative))
                    continue; // not an area-prefixed localization key (defensive)

                if (!resxCache.TryGetValue(resxRelative, out var neutral))
                {
                    neutral = ResxKeyScanner.ScanFile(RepoPath(resxRelative));
                    resxCache[resxRelative] = neutral;
                }

                var present = neutral.ContainsKey(usage.Key)
                    || (neutral.ContainsKey(usage.Key + "_One") && neutral.ContainsKey(usage.Key + "_Other"));

                if (!present)
                    missing.Add($"{Rel(file)}:{usage.LineNumber}  {usage.Key}  -> {resxRelative}");
            }
        }

        Assert.True(missing.Count == 0,
            "Code references localization keys with no neutral .resx entry:\n" + string.Join("\n", missing));
    }

    // --- Enum coverage ------------------------------------------------------------

    [Fact]
    public void Should_HaveEnumKey_When_MemberOfUserFacingEnum()
    {
        var enums = ResxKeyScanner.ScanFile(RepoPath(AreaResx["Enum"]));
        var missing = new List<string>();

        foreach (var enumType in UserFacingEnums)
            foreach (var member in UserFacingEnumScanner.GetMemberNames(enumType))
            {
                var key = UserFacingEnumScanner.BuildResourceKey(enumType, member);
                if (!enums.ContainsKey(key))
                    missing.Add(key);
            }

        Assert.True(missing.Count == 0,
            "EnumsResource.resx is missing Enum_<Type>_<Member> keys for user-facing enum members: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Should_NotRenderRawEnum_When_AppSourceScanned()
    {
        // A user-facing enum reached by a common accessor and rendered with ToString(), or a
        // hand-rolled Active/Inactive-style ternary — both bypass LocalizeEnum()/ILocalizer.Enum.
        var rawToString = new Regex(
            @"\.(Status|FeeType|PaymentMethod|PaymentType|AccountType|TaxCode|ReconciliationStatus|JournalEntryType|CurrentTheme|Theme)\s*\.ToString\(",
            RegexOptions.Compiled);
        var enumTernary = new Regex(
            @"\?\s*""(Active|Inactive|Light|Dark|Draft|Finalised)""\s*:\s*""(Active|Inactive|Light|Dark|Draft|Finalised)""",
            RegexOptions.Compiled);

        var findings = new List<string>();

        foreach (var file in EnumerateSourceFiles(Us2ScanDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;

                // A ternary that feeds a `value="..."` binding emits a culture-invariant
                // <option>/<select> value token, not display text — the same rule that keeps
                // report-filter option values invariant while only the label localises.
                var isOptionValueToken = line.Contains("value=\"@(", StringComparison.Ordinal)
                    || line.Contains("value=@(", StringComparison.Ordinal);

                if (rawToString.IsMatch(line) || (!isOptionValueToken && enumTernary.IsMatch(line)))
                    findings.Add($"{Rel(file)}:{i + 1}  {line.Trim()}");
            }
        }

        Assert.True(findings.Count == 0,
            "A user-facing enum is rendered without LocalizeEnum()/ILocalizer.Enum:\n" + string.Join("\n", findings));
    }

    // --- Residual literals ------------------------------------------------------------

    [Fact]
    public void Should_HaveNoUserFacingLiteral_When_AppSurfaceScanned()
    {
        // Same heuristics the per-module interim guards were converted against, applied to the
        // whole US1 + US2 surface: a text node with 2+ letters that is not a Razor expression;
        // an aria-label / alt / title / placeholder literal; a Capitalised multi-word C# string.
        var razorText = new Regex(@">\s*([A-Za-z][A-Za-z ,.'!?()\-]{2,})\s*<", RegexOptions.Compiled);
        var attrLiteral = new Regex(
            @"\b(aria-label|alt|title|placeholder)\s*=\s*""([^""@]*[A-Za-z]{3,}[^""]*)""",
            RegexOptions.Compiled);
        var csPhrase = new Regex(@"""([A-Z][a-z]+(?: [A-Za-z]+)+[.!?]?)""", RegexOptions.Compiled);

        var findings = new List<string>();

        foreach (var file in EnumerateSourceFiles(ResidualLiteralDirectories))
        {
            var isRazor = file.EndsWith(".razor", StringComparison.Ordinal);
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (isRazor)
                {
                    foreach (Match m in razorText.Matches(line))
                    {
                        var text = m.Groups[1].Value.Trim();
                        if (IsAllowedToken(text)) continue;
                        findings.Add($"{Rel(file)}:{i + 1}  text node \"{text}\"");
                    }

                    foreach (Match m in attrLiteral.Matches(line))
                        findings.Add($"{Rel(file)}:{i + 1}  {m.Groups[1].Value}=\"{m.Groups[2].Value}\"");
                }
                else
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;     // doc comment
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;      // line comment
                    if (line.Contains("Logger.Log", StringComparison.Ordinal)) continue;   // diagnostic log text (FR-007)
                    if (line.Contains("nameof(", StringComparison.Ordinal)) continue;      // symbol name, not display text
                    if (line.Contains("ErrorMessage", StringComparison.Ordinal)) continue; // DataAnnotations attr arg — compile-time constant, hardened in T060
                    // IValidatableObject ctor message (may sit on the line after `new ValidationResult(`) — same category.
                    if (line.Contains("new ValidationResult(", StringComparison.Ordinal)
                        || (i > 0 && lines[i - 1].TrimEnd().EndsWith("new ValidationResult(", StringComparison.Ordinal))) continue;

                    foreach (Match m in csPhrase.Matches(line))
                    {
                        var text = m.Groups[1].Value;
                        if (IsAllowedToken(text)) continue;
                        findings.Add($"{Rel(file)}:{i + 1}  string literal \"{text}\"");
                    }
                }
            }
        }

        Assert.True(findings.Count == 0,
            "The US1 + US2 surface still contains user-facing string literals (should come from resources):\n"
            + string.Join("\n", findings));
    }

    // --- Satellite integrity ----------------------------------------------------------

    [Fact]
    public void Should_HaveNoOrphanKey_When_SatelliteResxScanned()
    {
        var orphans = new List<string>();

        foreach (var (neutralPath, satellitePath) in EnumerateSatellitePairs())
        {
            var neutral = ResxKeyScanner.ScanFile(neutralPath);
            var satellite = ResxKeyScanner.ScanFile(satellitePath);

            foreach (var key in satellite.Keys)
                if (!neutral.ContainsKey(key))
                    orphans.Add($"{Rel(satellitePath)}  {key}  (no neutral entry in {Rel(neutralPath)})");
        }

        Assert.True(orphans.Count == 0,
            "A satellite .resx defines a key its neutral file does not:\n" + string.Join("\n", orphans));
    }

    // --- Placeholder / plural parity ------------------------------------------------

    [Fact]
    public void Should_PairAndAgree_When_PluralKeyDefined()
    {
        var problems = new List<string>();

        foreach (var resx in EnumerateNeutralResx())
        {
            var entries = ResxKeyScanner.ScanFile(resx);

            foreach (var key in entries.Keys)
            {
                // Enum members literally named "Other" (FeeType.Other, …) are not plural halves.
                if (key.StartsWith("Enum_", StringComparison.Ordinal)) continue;

                if (key.EndsWith("_One", StringComparison.Ordinal))
                {
                    var other = key[..^4] + "_Other";
                    if (!entries.ContainsKey(other))
                        problems.Add($"{Rel(resx)}  {key} has no matching {other}");
                    else
                        AssertTokensAgree(resx, key, entries[key], other, entries[other], problems);
                }
                else if (key.EndsWith("_Other", StringComparison.Ordinal))
                {
                    var one = key[..^6] + "_One";
                    if (!entries.ContainsKey(one))
                        problems.Add($"{Rel(resx)}  {key} has no matching {one}");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "Plural key pairing/placeholder parity violations:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Should_AgreeOnTokens_When_SatelliteTranslatesKey()
    {
        var problems = new List<string>();

        foreach (var (neutralPath, satellitePath) in EnumerateSatellitePairs())
        {
            var neutral = ResxKeyScanner.ScanFile(neutralPath);
            var satellite = ResxKeyScanner.ScanFile(satellitePath);

            foreach (var (key, translated) in satellite)
            {
                if (!neutral.TryGetValue(key, out var baseline)) continue; // orphan — reported elsewhere
                AssertTokensAgree(satellitePath, key, translated, key, baseline, problems, neutralPath);
            }
        }

        Assert.True(problems.Count == 0,
            "A satellite translation changed a key's named-placeholder set:\n" + string.Join("\n", problems));
    }

    // --- Currency formatting ------------------------------------------------------

    [Fact]
    public void Should_NotUseCFormat_When_AnyDisplaySiteScanned()
    {
        var cFormat = new Regex(
            @"ToString\(\s*""C\d?""\s*[,)]|""\{0:C\d?\}""|FormatString\s*=\s*""\{0:C",
            RegexOptions.Compiled);

        var findings = new List<string>();

        foreach (var file in EnumerateSourceFiles(["src"]))
        {
            var name = Path.GetFileName(file);
            if (name is "MoneyFormatter.cs") continue; // the sanctioned one "C" implementation

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!cFormat.IsMatch(line)) continue;
                if (line.Contains("newValue:", StringComparison.Ordinal)) continue;      // audit-trail record text (FR-007 exempt)
                if (line.Contains("Logger.Log", StringComparison.Ordinal)) continue;      // diagnostic log text (FR-007 exempt)
                if (line.Contains(".Log(", StringComparison.Ordinal)) continue;
                findings.Add($"{Rel(file)}:{i + 1}  {line.Trim()}");
            }
        }

        Assert.True(findings.Count == 0,
            "A monetary amount is formatted with the culture currency symbol (\"C\"); use MoneyFormatter:\n"
            + string.Join("\n", findings));
    }

    // --- Helpers ----------------------------------------------------------------------

    /// <summary>Punctuation/glyph-only runs, and Razor control words that are not display copy.</summary>
    private static bool IsAllowedToken(string text)
    {
        if (!text.Any(char.IsLetter)) return true;
        return text is "else" or "true" or "false";
    }

    private static void AssertTokensAgree(
        string filePath, string keyA, string valueA, string keyB, string valueB,
        List<string> problems, string? fileB = null)
    {
        var a = PlaceholderTokenScanner.ScanValue(valueA).Where(t => t != "Count").ToHashSet(StringComparer.Ordinal);
        var b = PlaceholderTokenScanner.ScanValue(valueB).Where(t => t != "Count").ToHashSet(StringComparer.Ordinal);
        if (!a.SetEquals(b))
            problems.Add(
                $"{Rel(filePath)}  {keyA} {{{string.Join(",", a.OrderBy(x => x))}}} != "
                + $"{(fileB is null ? "" : Rel(fileB) + " ")}{keyB} {{{string.Join(",", b.OrderBy(x => x))}}}");
    }

    private static IEnumerable<string> EnumerateSourceFiles(IEnumerable<string> relativeDirs)
    {
        foreach (var relative in relativeDirs)
        {
            var dir = RepoPath(relative);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;
                if (file.EndsWith(".razor", StringComparison.Ordinal)
                    || file.EndsWith(".cs", StringComparison.Ordinal))
                    yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateNeutralResx()
    {
        foreach (var relative in AreaResx.Values.Distinct())
            yield return RepoPath(relative);
    }

    private static IEnumerable<(string Neutral, string Satellite)> EnumerateSatellitePairs()
    {
        var satelliteName = new Regex(@"^(?<stem>[A-Za-z]+Resource)\.(?<culture>[a-z]{2}(-[A-Za-z]+)+|qps-[a-z]+)\.resx$", RegexOptions.Compiled);
        var src = RepoPath("src");

        foreach (var file in Directory.EnumerateFiles(src, "*.resx", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var m = satelliteName.Match(Path.GetFileName(file));
            if (!m.Success) continue;

            var neutral = Path.Combine(Path.GetDirectoryName(file)!, m.Groups["stem"].Value + ".resx");
            if (File.Exists(neutral))
                yield return (neutral, file);
        }
    }

    private static string Rel(string absolute) =>
        Path.GetRelativePath(RepoRoot(), absolute).Replace(Path.DirectorySeparatorChar, '/');

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

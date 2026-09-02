using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Localization.Tests.Scanning;
using StageFright.UI.Resources.Strings;

namespace StageFright.Localization.Tests;

/// <summary>
/// Localization guard suite scoped to the US1 slice (navigation shell + Members module).
/// Enforces the resource-key-catalog.md §3 contract for that slice: baseline completeness,
/// enum-key coverage (MemberStatus + Theme), no residual user-facing literals, no raw enum
/// rendering, no <c>"C"</c> currency format, and missing-key warning + neutral fallback.
/// US2 (T029) extends these repo-wide.
/// </summary>
public class Us1LocalizationGuardTests
{
    // --- Slice definition -----------------------------------------------------

    private static readonly string[] Us1RelativeFiles =
    [
        "src/StageFright.UI/Layout/ShellLayout.razor",
        "src/StageFright.UI/Layout/ShellLayout.razor.cs",
        "src/StageFright.UI/Layout/ThemeProvider.razor",
        "src/StageFright.UI/Layout/ThemeProvider.razor.cs",
        "src/StageFright.UI/Pages/Members/MemberList.razor",
        "src/StageFright.UI/Pages/Members/MemberList.razor.cs",
        "src/StageFright.UI/Pages/Members/MemberDetail.razor",
        "src/StageFright.UI/Pages/Members/MemberDetail.razor.cs",
        "src/StageFright.UI/Pages/Members/MemberForm.razor",
        "src/StageFright.UI/Pages/Members/MemberForm.razor.cs",
        "src/StageFright.UI/Modules/Members/MembersTile.razor",
        "src/StageFright.UI/Modules/Members/MembersTile.razor.cs",
        "src/StageFright.UI/Modules/Members/MembersDashboardTileProvider.cs",
        "src/StageFright.Core/Modules/Members/MemberMenuItemProvider.cs",
        "src/StageFright.Core/Modules/Members/MemberValidationService.cs",
    ];

    private static readonly Dictionary<string, string> AreaResx = new()
    {
        ["Nav"] = "src/StageFright.Core/Modules/Localization/Resources/NavigationResource.resx",
        ["Validation"] = "src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx",
        ["Enum"] = "src/StageFright.Core/Modules/Localization/Resources/EnumsResource.resx",
        ["Members"] = "src/StageFright.UI/Resources/Strings/MembersResource.resx",
        ["Shared"] = "src/StageFright.UI/Resources/Strings/SharedResource.resx",
    };

    // --- Guards -------------------------------------------------------------------

    [Fact]
    public void Should_HaveNeutralEntry_When_KeyReferencedInCode()
    {
        var missing = new List<string>();

        foreach (var relative in Us1RelativeFiles)
        {
            var path = RepoPath(relative);
            foreach (var usage in LocalizerKeyUsageScanner.ScanFile(path))
            {
                var area = usage.Key.Split('_', 2)[0];
                if (!AreaResx.TryGetValue(area, out var resxRelative))
                    continue; // not an area-prefixed localization key (defensive)

                var neutral = ResxKeyScanner.ScanFile(RepoPath(resxRelative));

                // A key passed to ILocalizer.Plural<T> resolves to "<key>_One" / "<key>_Other";
                // accept it when the neutral set carries both plural forms (resource-key-catalog.md §3).
                var present = neutral.ContainsKey(usage.Key)
                    || (neutral.ContainsKey(usage.Key + "_One") && neutral.ContainsKey(usage.Key + "_Other"));

                if (!present)
                    missing.Add($"{usage.Key}  ({relative}:{usage.LineNumber})  -> {resxRelative}");
            }
        }

        Assert.True(missing.Count == 0,
            "US1 code references localization keys with no neutral .resx entry:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Should_HaveEnumKey_When_MemberOfUs1UserFacingEnum()
    {
        var enums = ResxKeyScanner.ScanFile(RepoPath(AreaResx["Enum"]));
        var missing = new List<string>();

        foreach (var enumType in new[] { typeof(MemberStatus), typeof(Theme) })
        {
            foreach (var member in UserFacingEnumScanner.GetMemberNames(enumType))
            {
                var key = UserFacingEnumScanner.BuildResourceKey(enumType, member);
                if (!enums.ContainsKey(key))
                    missing.Add(key);
            }
        }

        Assert.True(missing.Count == 0,
            "EnumsResource.resx is missing keys for US1 user-facing enum members: " + string.Join(", ", missing));
    }

    [Fact]
    public void Should_HaveNoUserFacingLiteral_When_Us1FileScanned()
    {
        // Text node with 2+ consecutive letters that is NOT a Razor expression / binding.
        var razorText = new Regex(@">\s*([A-Za-z][A-Za-z ,.'!?()\-]{2,})\s*<", RegexOptions.Compiled);
        // aria-label / alt / title / placeholder literal (no '@' -> not a bound expression).
        var attrLiteral = new Regex(
            @"\b(aria-label|alt|title|placeholder)\s*=\s*""([^""@]*[A-Za-z]{3,}[^""]*)""",
            RegexOptions.Compiled);
        // A Capitalised multi-word phrase inside a C# string literal (e.g. "Add Member").
        var csPhrase = new Regex(@"""([A-Z][a-z]+(?: [A-Za-z]+)+[.!?]?)""", RegexOptions.Compiled);

        var findings = new List<string>();

        foreach (var relative in Us1RelativeFiles)
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
            "US1 slice still contains user-facing string literals (should come from resources):\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_NotRenderRawEnum_When_Us1RazorScanned()
    {
        var banned = new[]
        {
            "\"Active\" : \"Inactive\"",
            "\"Inactive\" : \"Active\"",
            "\"Light\" : \"Dark\"",
            "\"Dark\" : \"Light\"",
            "\" (Inactive)\"",
            ".Status.ToString(",
            ".Status</",
            "CurrentTheme.ToString(",
        };

        var findings = new List<string>();
        foreach (var relative in Us1RelativeFiles.Where(f => f.EndsWith(".razor", StringComparison.Ordinal)))
        {
            var content = File.ReadAllText(RepoPath(relative));
            foreach (var pattern in banned)
                if (content.Contains(pattern, StringComparison.Ordinal))
                    findings.Add($"{relative}  contains raw-enum render `{pattern}`");
        }

        Assert.True(findings.Count == 0,
            "US1 razor renders an enum without LocalizeEnum():\n" + string.Join("\n", findings));
    }

    [Fact]
    public void Should_NotUseCFormat_When_Us1FileScanned()
    {
        var cFormat = new Regex(
            @"ToString\(\s*""C\d?""\s*\)|""\{0:C\d?\}""|FormatString\s*=\s*""\{0:C",
            RegexOptions.Compiled);

        var findings = new List<string>();
        foreach (var relative in Us1RelativeFiles)
        {
            var lines = File.ReadAllLines(RepoPath(relative));
            for (var i = 0; i < lines.Length; i++)
                if (cFormat.IsMatch(lines[i]))
                    findings.Add($"{relative}:{i + 1}  {lines[i].Trim()}");
        }

        Assert.True(findings.Count == 0,
            "US1 slice formats a monetary amount with the culture currency symbol (\"C\"); use MoneyFormatter:\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_LogWarningAndFallBack_When_KeyMissingForActiveCulture()
    {
        var logger = new CapturingLogger<MissingKeyLoggingLocalizerFactory>();
        var inner = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        var factory = new MissingKeyLoggingLocalizerFactory(inner, logger);

        var localizer = factory.Create(typeof(MembersResource));
        var result = localizer["Members_Guard_KeyThatDoesNotExist"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("Members_Guard_KeyThatDoesNotExist", result.Value); // neutral fallback = key name, never blank
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Missing localization key", StringComparison.Ordinal));
    }

    // --- Helpers ---------------------------------------------------------------

    private static bool IsAllowedToken(string text)
    {
        // Punctuation-only / glyph runs (em dash, asterisk, etc.).
        if (!text.Any(char.IsLetter)) return true;
        return false;
    }

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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<(LogLevel Level, string Message)> Entries = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}

using System.Text.RegularExpressions;
using StageFright.Localization.Tests.Scanning;

namespace StageFright.Localization.Tests;

/// <summary>
/// Interim localization guard scoped to the non-Finance Core services converted in US2, T041
/// (Agm, Events, committee office-holder titles, Rehearsals attendance, Settings, Setup, Backup).
/// Enforces the resource-key-catalog.md §3 contract for user-facing exception <c>Message</c>
/// text on that slice: every <c>ValidationResource</c> key referenced has a neutral (en-AU)
/// entry, and no bare English literal remains as the message argument of a user-facing
/// exception. The Finance services and the repo-wide unscoping stay with T041 (Finance) / T029.
/// </summary>
public class Us2ExceptionMessageGuardTests
{
    private static readonly string[] NonFinanceServiceFiles =
    [
        "src/StageFright.Core/Modules/Agm/AgmService.cs",
        "src/StageFright.Core/Modules/Events/EventService.cs",
        "src/StageFright.Core/Modules/Events/EventTypeService.cs",
        "src/StageFright.Core/Modules/Members/CommitteeOfficeHolderTypeService.cs",
        "src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs",
        "src/StageFright.Core/Modules/Settings/SettingsService.cs",
        "src/StageFright.Core/Modules/Settings/SetupService.cs",
        "src/StageFright.Core/Modules/Settings/BackupService.cs",
    ];

    private const string ValidationResx =
        "src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx";

    /// <summary>
    /// A string literal as the first argument of a user-facing exception constructor — i.e.
    /// a hardcoded message that has not been routed through <c>_localizer.Get&lt;ValidationResource&gt;</c>.
    /// </summary>
    private static readonly Regex LiteralExceptionMessage = new(
        @"throw new (?:Validation|DataIntegrity|Reconciliation|Import)Exception\(\s*(""(?:[^""\\]|\\.)*"")",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void Should_HaveNeutralEntry_When_ValidationKeyReferencedInNonFinanceService()
    {
        var neutral = ResxKeyScanner.ScanFile(RepoPath(ValidationResx));
        var missing = new List<string>();

        foreach (var relative in NonFinanceServiceFiles)
        {
            foreach (var usage in LocalizerKeyUsageScanner.ScanFile(RepoPath(relative)))
            {
                if (!usage.Key.StartsWith("Validation_", StringComparison.Ordinal))
                    continue;

                if (!neutral.ContainsKey(usage.Key))
                    missing.Add($"{usage.Key}  ({relative}:{usage.LineNumber})");
            }
        }

        Assert.True(missing.Count == 0,
            "Non-Finance service references a ValidationResource key with no neutral .resx entry:\n"
            + string.Join("\n", missing));
    }

    [Fact]
    public void Should_HaveNoUserFacingLiteral_When_NonFinanceServiceExceptionScanned()
    {
        var findings = new List<string>();

        foreach (var relative in NonFinanceServiceFiles)
        {
            var text = File.ReadAllText(RepoPath(relative));
            foreach (Match m in LiteralExceptionMessage.Matches(text))
            {
                var literal = m.Groups[1].Value;
                if (literal.Any(char.IsLetter))
                    findings.Add($"{relative}  {literal}");
            }
        }

        Assert.True(findings.Count == 0,
            "Non-Finance service still throws a user-facing exception with a hardcoded message "
            + "(route it through _localizer.Get<ValidationResource>):\n" + string.Join("\n", findings));
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
}

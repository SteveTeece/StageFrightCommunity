using System.Text.RegularExpressions;
using StageFright.Localization.Tests.Scanning;

namespace StageFright.Localization.Tests;

/// <summary>
/// Localization guard for the user-facing exception <c>Message</c> text of the US2 (T041)
/// application-service slice — the non-Finance services (Agm, Events, committee office-holder
/// titles, Rehearsals attendance, Settings, Setup, Backup) plus every Finance service
/// (Account, BankReconciliation, ExpensePayment, Payment, Fee, GeneralJournal, IncomeEntry,
/// BankDeposit, OpeningBalance). Enforces the resource-key-catalog.md §3 contract for that
/// slice: every <c>ValidationResource</c> key referenced has a neutral (en-AU) entry, and no
/// bare English literal remains as the message argument of a user-facing exception. The
/// repo-wide residual-literal / enum / placeholder guards stay with T029.
/// </summary>
public class Us2ExceptionMessageGuardTests
{
    private static readonly string[] Us2ServiceFiles =
    [
        "src/StageFright.Core/Modules/Agm/AgmService.cs",
        "src/StageFright.Core/Modules/Events/EventService.cs",
        "src/StageFright.Core/Modules/Events/EventTypeService.cs",
        "src/StageFright.Core/Modules/Members/CommitteeOfficeHolderTypeService.cs",
        "src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs",
        "src/StageFright.Core/Modules/Settings/SettingsService.cs",
        "src/StageFright.Core/Modules/Settings/SetupService.cs",
        "src/StageFright.Core/Modules/Settings/BackupService.cs",
        "src/StageFright.Core/Modules/Finance/AccountService.cs",
        "src/StageFright.Core/Modules/Finance/BankReconciliationService.cs",
        "src/StageFright.Core/Modules/Finance/ExpensePaymentService.cs",
        "src/StageFright.Core/Modules/Finance/PaymentService.cs",
        "src/StageFright.Core/Modules/Finance/FeeService.cs",
        "src/StageFright.Core/Modules/Finance/GeneralJournalService.cs",
        "src/StageFright.Core/Modules/Finance/IncomeEntryService.cs",
        "src/StageFright.Core/Modules/Finance/BankDepositService.cs",
        "src/StageFright.Core/Modules/Finance/OpeningBalanceService.cs",
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
    public void Should_HaveNeutralEntry_When_ValidationKeyReferencedInUs2Service()
    {
        var neutral = ResxKeyScanner.ScanFile(RepoPath(ValidationResx));
        var missing = new List<string>();

        foreach (var relative in Us2ServiceFiles)
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
    public void Should_HaveNoUserFacingLiteral_When_Us2ServiceExceptionScanned()
    {
        var findings = new List<string>();

        foreach (var relative in Us2ServiceFiles)
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

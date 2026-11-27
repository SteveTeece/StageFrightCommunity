using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Reports;

/// <summary>
/// On-screen body of Reports → Bank Deposits (issue #399): every deposit, newest first, each
/// opening its read-only report page. Fed from the same Banking history the report provider
/// reads; ReportViewer recreates this component on every (re)generation, so loading once in
/// <see cref="OnInitializedAsync"/> is fresh.
/// </summary>
public partial class BankDepositsReportView : ComponentBase
{
    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<BankDepositsReportView> Logger { get; set; } = null!;

    private IReadOnlyList<BankingHistoryEntry>? _deposits;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _deposits = (await BankingService.GetHistoryAsync())
                .Where(e => e.Type == BankingHistoryEntryType.Deposit)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load the bank deposits report");
            _error = Loc.Get<SharedResource>("Shared_Report_GenerateError", ex.GetType().Name);
        }
    }

    private static string DepositRoute(BankingHistoryEntry entry) => $"/reports/bank-deposits/{entry.Id}";

    private string LinkText(BankingHistoryEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Description) ? L["Finance_Banking_DepositDetailTitle"] : entry.Description;

    private string StatusText(BankingHistoryEntry entry) =>
        entry.IsReversed ? L["Finance_Banking_Status_Reversed"] : L["Finance_Banking_Status_Recorded"];
}

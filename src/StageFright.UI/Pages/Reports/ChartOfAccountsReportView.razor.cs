using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Reports;

/// <summary>
/// On-screen body of Reports → Chart of Accounts (issue #367): the same read-only
/// <see cref="Shared.ChartOfAccountsGrid"/> Finance → Chart of Accounts shows, fed from the same
/// <see cref="IAccountBalanceService"/> the report provider reads. ReportViewer recreates this
/// component on every (re)generation, so loading once in <see cref="OnInitializedAsync"/> is fresh.
/// </summary>
public partial class ChartOfAccountsReportView : ComponentBase
{
    [Inject] private IAccountBalanceService AccountBalanceService { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    [Parameter] public bool IncludeBalances { get; set; }

    private IReadOnlyList<AccountBalance>? _accounts;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await AccountBalanceService.GetActiveAccountBalancesAsync();
        }
        catch (Exception ex)
        {
            _error = Loc.Get<SharedResource>("Shared_Report_GenerateError", ex.GetType().Name);
        }
    }
}

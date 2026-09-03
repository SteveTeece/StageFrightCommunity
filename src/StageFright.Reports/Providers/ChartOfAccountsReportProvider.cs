using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Chart of Accounts report — every active account grouped into the fixed
/// Assets/Liabilities/Equity/Income/Expenses sections, ordered by AccountNumber within each,
/// sourced from the same IAccountBalanceService the Chart of Accounts screen already uses.
/// Never reads archived accounts and never shows a combined grand-total (types mix
/// debit-normal and credit-normal balances, so summing them would not be meaningful).
/// </summary>
public class ChartOfAccountsReportProvider : IReportProvider
{
    private readonly IAccountBalanceService _balanceService;
    private readonly ILocalizer _localizer;

    public ChartOfAccountsReportProvider(IAccountBalanceService balanceService, ILocalizer localizer)
    {
        _balanceService = balanceService;
        _localizer = localizer;
    }

    public string ReportId => "chart-of-accounts";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 15;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
        [new ReportFilterDefinition { Key = "includeBalances", Type = ReportFilterType.Boolean, Label = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_IncludeBalancesFilterLabel"), DefaultValue = "false" }];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var includeBalances = filters.Get("includeBalances") == "true";
        var balances = await _balanceService.GetActiveAccountBalancesAsync(ct);

        List<ReportRow> RowsFor(AccountType type) => balances
            .Where(a => a.Type == type)
            .OrderBy(a => a.AccountNumber)
            .Select(a => new ReportRow { Cells = RowCells(a, includeBalances) })
            .ToList();

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_Name"),
            Columns = includeBalances
                ?
                [
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NumberColumn") },
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NameColumn") },
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Balance") }
                ]
                :
                [
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NumberColumn") },
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NameColumn") }
                ],
            Sections =
            [
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Assets"), Rows = RowsFor(AccountType.Asset) },
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Liabilities"), Rows = RowsFor(AccountType.Liability) },
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Equity"), Rows = RowsFor(AccountType.Equity) },
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Income"), Rows = RowsFor(AccountType.Income) },
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Expenses"), Rows = RowsFor(AccountType.Expense) }
            ],
            GrandTotal = null,
            SummaryColumns = null
        };
    }

    private IReadOnlyList<string> RowCells(AccountBalance a, bool includeBalances) => includeBalances
        ? [a.AccountNumber, FormatName(a), FormatBalance(a)]
        : [a.AccountNumber, FormatName(a)];

    private string FormatName(AccountBalance a)
    {
        if (a.IsSystem && a.IsBankAccount) return _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NameSystemBank", a.Name);
        if (a.IsSystem) return _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NameSystem", a.Name);
        if (a.IsBankAccount) return _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_NameBank", a.Name);
        return a.Name;
    }

    private string FormatBalance(AccountBalance a) =>
        a.HasError || a.Balance is not { } balance
            ? _localizer.Get<ReportsResource>("Reports_ChartOfAccounts_BalanceError")
            : MoneyFormatter.Format(balance);
}

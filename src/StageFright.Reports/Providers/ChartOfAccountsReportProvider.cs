using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

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

    public ChartOfAccountsReportProvider(IAccountBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    public string ReportId => "chart-of-accounts";
    public string ReportName => "Chart of Accounts";
    public string ModuleName => "Finance";
    public int DisplayOrder => 15;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
        [new ReportFilterDefinition { Key = "includeBalances", Type = ReportFilterType.Boolean, Label = "Include Current Balances", DefaultValue = "false" }];

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
            Title = "Chart of Accounts",
            Columns = includeBalances
                ?
                [
                    new ReportColumn { Header = "No." },
                    new ReportColumn { Header = "Name" },
                    new ReportColumn { Header = "Balance" }
                ]
                :
                [
                    new ReportColumn { Header = "No." },
                    new ReportColumn { Header = "Name" }
                ],
            Sections =
            [
                new ReportSection { Heading = "Assets", Rows = RowsFor(AccountType.Asset) },
                new ReportSection { Heading = "Liabilities", Rows = RowsFor(AccountType.Liability) },
                new ReportSection { Heading = "Equity", Rows = RowsFor(AccountType.Equity) },
                new ReportSection { Heading = "Income", Rows = RowsFor(AccountType.Income) },
                new ReportSection { Heading = "Expenses", Rows = RowsFor(AccountType.Expense) }
            ],
            GrandTotal = null,
            SummaryColumns = null
        };
    }

    private static IReadOnlyList<string> RowCells(AccountBalance a, bool includeBalances) => includeBalances
        ? [a.AccountNumber, FormatName(a), FormatBalance(a)]
        : [a.AccountNumber, FormatName(a)];

    private static string FormatName(AccountBalance a)
    {
        if (a.IsSystem && a.IsBankAccount) return $"{a.Name} (System, Bank)";
        if (a.IsSystem) return $"{a.Name} (System)";
        if (a.IsBankAccount) return $"{a.Name} (Bank)";
        return a.Name;
    }

    private static string FormatBalance(AccountBalance a) => a.HasError ? "Error" : a.Balance?.ToString("F2") ?? "Error";
}

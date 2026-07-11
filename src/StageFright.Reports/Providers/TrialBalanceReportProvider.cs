using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Trial Balance report.
/// Sections: Assets, Liabilities, Equity, Income, Expenses — grouped by AccountId
/// (the denormalized Transaction.GLAccount string is a posting-time snapshot and is
/// never used for aggregation). Default date range is the current financial year.
/// Throws GLBalanceException if |TotalDebits − TotalCredits| > 0.01 per FR-034.
/// </summary>
public class TrialBalanceReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;

    public TrialBalanceReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
    }

    public string ReportId => "trial-balance";
    public string ReportName => "Trial Balance";
    public string ModuleName => "Finance";
    public int DisplayOrder => 20;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (from, to) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From", DefaultValue = $"{from:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To", DefaultValue = $"{to:yyyy-MM-dd}" }
            ];
        }
    }

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = await ParseDateRangeAsync(filters, ct);
        var (totalDebits, totalCredits) = await _gl.GetBalanceTotalsAsync(from, to, ct);

        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
        {
            throw new GLBalanceException(
                $"GL Balance Verification Failed: Total Debits (${totalDebits:F2}) ≠ Total Credits (${totalCredits:F2}). " +
                "The Trial Balance cannot be generated while the ledger is out of balance.",
                "Transaction",
                "TrialBalance");
        }

        var allAccounts = (await _accounts.GetAllAsync(ct))
            .Concat(await _accounts.GetArchivedAsync(ct))
            .ToList();
        var movements = await _gl.GetAccountMovementsAsync(from, to, ct);

        // System asset accounts (Cash, Member Receivable) always show; other accounts show
        // when active (non-archived user accounts) or when they had movement in the range.
        List<ReportRow> RowsFor(AccountType type) => allAccounts
            .Where(a => a.Type == type)
            .OrderBy(a => a.AccountNumber)
            .Select(a => (Account: a, Movement: movements.GetValueOrDefault(a.Id)))
            .Where(x =>
                (x.Account.IsSystem && x.Account.Type == AccountType.Asset)
                || (!x.Account.IsSystem && !x.Account.IsDeleted)
                || x.Movement.Debits != 0m || x.Movement.Credits != 0m)
            .Select(x => new ReportRow
            {
                Cells =
                [
                    $"{x.Account.Name} ({x.Account.AccountNumber})",
                    FormatCurrency(x.Movement.Debits),
                    FormatCurrency(x.Movement.Credits)
                ]
            })
            .ToList();

        return new ReportData
        {
            Title = "Trial Balance",
            SubTitle = $"{from:d MMMM yyyy} – {to:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Debit", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency },
                new ReportColumn { Header = "Credit", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
            ],
            Sections =
            [
                new ReportSection { Heading = "Assets", Rows = RowsFor(AccountType.Asset) },
                new ReportSection { Heading = "Liabilities", Rows = RowsFor(AccountType.Liability) },
                new ReportSection { Heading = "Equity", Rows = RowsFor(AccountType.Equity) },
                new ReportSection { Heading = "Income", Rows = RowsFor(AccountType.Income) },
                new ReportSection { Heading = "Expenses", Rows = RowsFor(AccountType.Expense) }
            ],
            GrandTotal = new ReportRow
            {
                Cells = ["Totals", FormatCurrency(totalDebits), FormatCurrency(totalCredits)],
                IsEmphasized = true
            }
        };
    }

    private async Task<(DateTime From, DateTime To)> ParseDateRangeAsync(ReportFilterValues filters, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var startMonth = settings?.FinancialYearStartMonth ?? FinancialYearCalculator.DefaultStartMonth;
        var (fyFrom, fyTo) = FinancialYearCalculator.GetRange(DateTime.UtcNow, startMonth);

        var from = DateTime.TryParse(filters.Get("dateFrom"), out var df)
            ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc)
            : fyFrom;
        var to = DateTime.TryParse(filters.Get("dateTo"), out var dt)
            ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc)
            : fyTo;
        return (from, to);
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("F2");
}

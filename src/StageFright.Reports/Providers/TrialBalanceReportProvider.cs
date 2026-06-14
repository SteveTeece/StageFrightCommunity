using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Trial Balance report.
/// Sections: Assets (Cash GL#0100, MemberReceivable GL#0101), Income categories, Expense categories.
/// Throws GLBalanceException if |TotalDebits − TotalCredits| > 0.01 per FR-034.
/// </summary>
public class TrialBalanceReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly ICategoryRepository _categories;

    public TrialBalanceReportProvider(IGLRepository gl, ICategoryRepository categories)
    {
        _gl = gl;
        _categories = categories;
    }

    public string ReportId => "trial-balance";
    public string ReportName => "Trial Balance";
    public string ModuleName => "Finance";
    public int DisplayOrder => 20;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From", DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To", DefaultValue = $"{DateTime.UtcNow.Year}-12-31" }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = ParseDateRange(filters);
        var (totalDebits, totalCredits) = await _gl.GetBalanceTotalsAsync(from, to, ct);

        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
        {
            throw new GLBalanceException(
                $"GL Balance Verification Failed: Total Debits (${totalDebits:F2}) ≠ Total Credits (${totalCredits:F2}). " +
                "The Trial Balance cannot be generated while the ledger is out of balance.",
                "Transaction",
                "TrialBalance");
        }

        var allCategories = await _categories.GetAllAsync(ct);
        var transactions = await _gl.GetByDateRangeAsync(from, to, ct);
        var txnsByGl = transactions.GroupBy(t => t.GLAccount)
            .ToDictionary(g => g.Key, g => g.ToList());

        decimal CatDebit(string glAccount) => txnsByGl.GetValueOrDefault(glAccount, []).Sum(t => t.DebitAmount);
        decimal CatCredit(string glAccount) => txnsByGl.GetValueOrDefault(glAccount, []).Sum(t => t.CreditAmount);

        var assetRows = new List<ReportRow>
        {
            new() { Cells = ["Cash (0100)", FormatCurrency(CatDebit("0100")), FormatCurrency(CatCredit("0100"))] },
            new() { Cells = ["Member Receivable (0101)", FormatCurrency(CatDebit("0101")), FormatCurrency(CatCredit("0101"))] }
        };

        var incomeRows = allCategories
            .Where(c => c.Type == CategoryType.Income && !c.IsSystem)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .Select(c => new ReportRow { Cells = [$"{c.Name} ({c.GLAccount})", FormatCurrency(CatDebit(c.GLAccount)), FormatCurrency(CatCredit(c.GLAccount))] })
            .ToList();

        var expenseRows = allCategories
            .Where(c => c.Type == CategoryType.Expense && !c.IsSystem)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .Select(c => new ReportRow { Cells = [$"{c.Name} ({c.GLAccount})", FormatCurrency(CatDebit(c.GLAccount)), FormatCurrency(CatCredit(c.GLAccount))] })
            .ToList();

        var badDebtDebit = CatDebit("9900");
        var badDebtCredit = CatCredit("9900");
        if (badDebtDebit > 0 || badDebtCredit > 0)
            expenseRows.Add(new ReportRow { Cells = ["Bad Debt Expense (9900)", FormatCurrency(badDebtDebit), FormatCurrency(badDebtCredit)] });

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
                new ReportSection { Heading = "Assets", Rows = assetRows },
                new ReportSection { Heading = "Income", Rows = incomeRows },
                new ReportSection { Heading = "Expenses", Rows = expenseRows }
            ],
            GrandTotal = new ReportRow
            {
                Cells = ["Totals", FormatCurrency(totalDebits), FormatCurrency(totalCredits)],
                IsEmphasized = true
            }
        };
    }

    private static (DateTime From, DateTime To) ParseDateRange(ReportFilterValues filters)
    {
        var year = DateTime.UtcNow.Year;
        var from = DateTime.TryParse(filters.Get("dateFrom"), out var df)
            ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc)
            : new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = DateTime.TryParse(filters.Get("dateTo"), out var dt)
            ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc)
            : new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        return (from, to);
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("F2");
}

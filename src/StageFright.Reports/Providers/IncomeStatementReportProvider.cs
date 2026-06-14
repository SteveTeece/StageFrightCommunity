using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Income Statement report for the Finance module.
/// Income section: one row per income category with subtotal.
/// Expenses section: one row per expense category with subtotal.
/// Grand total: Net Income (positive) or Net Loss (negative).
/// </summary>
public class IncomeStatementReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly ICategoryRepository _categories;

    public IncomeStatementReportProvider(IGLRepository gl, ICategoryRepository categories)
    {
        _gl = gl;
        _categories = categories;
    }

    public string ReportId => "income-statement";
    public string ReportName => "Income Statement";
    public string ModuleName => "Finance";
    public int DisplayOrder => 10;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From", DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To", DefaultValue = $"{DateTime.UtcNow.Year}-12-31" }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = ParseDateRange(filters);
        var allCategories = await _categories.GetAllAsync(ct);
        var transactions = await _gl.GetByDateRangeAsync(from, to, ct);

        var txnsByCat = transactions.GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var incomeCategories = allCategories
            .Where(c => c.Type == CategoryType.Income && !c.IsSystem)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToList();

        var expenseCategories = allCategories
            .Where(c => c.Type == CategoryType.Expense && !c.IsSystem)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToList();

        decimal totalIncome = 0m;
        var incomeRows = new List<ReportRow>();
        foreach (var cat in incomeCategories)
        {
            var catTxns = txnsByCat.GetValueOrDefault(cat.Id, []);
            var amount = catTxns.Sum(t => t.CreditAmount) - catTxns.Sum(t => t.DebitAmount);
            totalIncome += amount;
            incomeRows.Add(new ReportRow { Cells = [cat.Name, FormatCurrency(amount)] });
        }

        decimal totalExpenses = 0m;
        var expenseRows = new List<ReportRow>();
        foreach (var cat in expenseCategories)
        {
            var catTxns = txnsByCat.GetValueOrDefault(cat.Id, []);
            var amount = catTxns.Sum(t => t.DebitAmount) - catTxns.Sum(t => t.CreditAmount);
            totalExpenses += amount;
            expenseRows.Add(new ReportRow { Cells = [cat.Name, FormatCurrency(amount)] });
        }

        var netIncome = totalIncome - totalExpenses;

        return new ReportData
        {
            Title = "Income Statement",
            SubTitle = $"{from:d MMMM yyyy} – {to:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left, Format = ReportColumnFormat.Text },
                new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
            ],
            Sections =
            [
                new ReportSection
                {
                    Heading = "Income",
                    Rows = incomeRows,
                    Subtotal = new ReportRow { Cells = ["Total Income", FormatCurrency(totalIncome)], IsEmphasized = true }
                },
                new ReportSection
                {
                    Heading = "Expenses",
                    Rows = expenseRows,
                    Subtotal = new ReportRow { Cells = ["Total Expenses", FormatCurrency(totalExpenses)], IsEmphasized = true }
                }
            ],
            GrandTotal = new ReportRow
            {
                Cells = [netIncome >= 0 ? "Net Income" : "Net Loss", FormatCurrency(netIncome)],
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

using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Statement of Income &amp; Expenditure. Period is chosen via an FY preset
/// (This FY / Last FY / Custom) aligned to Settings.FinancialYearStartMonth, with an
/// optional prior-year comparison column (the chosen period shifted back one year).
/// Income section: one row per income account with subtotal.
/// Expenses section: one row per expense account with subtotal.
/// Grand total: Surplus (positive) or (Deficit) (negative).
/// </summary>
public class IncomeStatementReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;

    public IncomeStatementReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
    }

    public string ReportId => "income-statement";
    public string ReportName => "Statement of Income & Expenditure";
    public string ModuleName => "Finance";
    public int DisplayOrder => 10;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (from, to) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "period", Type = ReportFilterType.Select, Label = "Period", Options = ["This FY", "Last FY", "Custom"], DefaultValue = "This FY" },
                new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From (custom period)", DefaultValue = $"{from:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To (custom period)", DefaultValue = $"{to:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "compare", Type = ReportFilterType.Boolean, Label = "Show prior-year comparison", DefaultValue = "false" }
            ];
        }
    }

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);
        var startMonth = settings?.FinancialYearStartMonth ?? FinancialYearCalculator.DefaultStartMonth;

        var (from, to) = ResolvePeriod(filters, startMonth);
        var compare = bool.TryParse(filters.Get("compare"), out var cmp) && cmp;

        var allAccounts = (await _accounts.GetAllAsync(ct)).Concat(await _accounts.GetArchivedAsync(ct)).ToList();
        var incomeAccounts = allAccounts.Where(a => a.Type == AccountType.Income).OrderBy(a => a.AccountNumber).ToList();
        var expenseAccounts = allAccounts.Where(a => a.Type == AccountType.Expense).OrderBy(a => a.AccountNumber).ToList();

        var current = await _gl.GetAccountMovementsAsync(from, to, ct);

        DateTime priorFrom = default, priorTo = default;
        IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)> prior =
            new Dictionary<Guid, (decimal Debits, decimal Credits)>();

        if (compare)
        {
            priorFrom = from.AddYears(-1);
            priorTo = to.AddYears(-1);
            prior = await _gl.GetAccountMovementsAsync(priorFrom, priorTo, ct);
        }

        var (incomeRows, totalIncome, priorTotalIncome) = BuildRows(incomeAccounts, current, prior, compare, creditNormal: true);
        var (expenseRows, totalExpenses, priorTotalExpenses) = BuildRows(expenseAccounts, current, prior, compare, creditNormal: false);

        var surplus = totalIncome - totalExpenses;
        var priorSurplus = priorTotalIncome - priorTotalExpenses;

        List<ReportColumn> columns = compare
            ?
            [
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Current Period", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency },
                new ReportColumn { Header = "Prior Period", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
            ]
            :
            [
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
            ];

        ReportRow TotalRow(string label, decimal amount, decimal priorAmount) => new()
        {
            Cells = compare
                ? [label, FormatCurrency(amount), FormatCurrency(priorAmount)]
                : [label, FormatCurrency(amount)],
            IsEmphasized = true
        };

        return new ReportData
        {
            Title = "Statement of Income & Expenditure",
            SubTitle = compare
                ? $"{from:d MMMM yyyy} – {to:d MMMM yyyy} (compared to {priorFrom:d MMMM yyyy} – {priorTo:d MMMM yyyy})"
                : $"{from:d MMMM yyyy} – {to:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns = columns,
            Sections =
            [
                new ReportSection { Heading = "Income", Rows = incomeRows, Subtotal = TotalRow("Total Income", totalIncome, priorTotalIncome) },
                new ReportSection { Heading = "Expenses", Rows = expenseRows, Subtotal = TotalRow("Total Expenses", totalExpenses, priorTotalExpenses) }
            ],
            GrandTotal = TotalRow(surplus >= 0 ? "Surplus" : "(Deficit)", surplus, priorSurplus)
        };
    }

    private static (List<ReportRow> Rows, decimal Total, decimal PriorTotal) BuildRows(
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)> current,
        IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)> prior,
        bool compare,
        bool creditNormal)
    {
        var rows = new List<ReportRow>();
        decimal total = 0m, priorTotal = 0m;

        foreach (var account in accounts)
        {
            var (debits, credits) = current.GetValueOrDefault(account.Id);
            var amount = creditNormal ? credits - debits : debits - credits;
            total += amount;

            if (compare)
            {
                var (priorDebits, priorCredits) = prior.GetValueOrDefault(account.Id);
                var priorAmount = creditNormal ? priorCredits - priorDebits : priorDebits - priorCredits;
                priorTotal += priorAmount;
                rows.Add(new ReportRow { Cells = [account.Name, FormatCurrency(amount), FormatCurrency(priorAmount)] });
            }
            else
            {
                rows.Add(new ReportRow { Cells = [account.Name, FormatCurrency(amount)] });
            }
        }

        return (rows, total, priorTotal);
    }

    private static (DateTime From, DateTime To) ResolvePeriod(ReportFilterValues filters, int startMonth)
    {
        var period = filters.Get("period");

        if (string.Equals(period, "Last FY", StringComparison.OrdinalIgnoreCase))
            return FinancialYearCalculator.GetPreviousRange(DateTime.UtcNow, startMonth);

        if (string.Equals(period, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            var (fyFrom, fyTo) = FinancialYearCalculator.GetRange(DateTime.UtcNow, startMonth);
            var from = DateTime.TryParse(filters.Get("dateFrom"), out var df)
                ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc)
                : fyFrom;
            var to = DateTime.TryParse(filters.Get("dateTo"), out var dt)
                ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc)
                : fyTo;
            return (from, to);
        }

        // "This FY" (default / unrecognised value)
        return FinancialYearCalculator.GetRange(DateTime.UtcNow, startMonth);
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("F2");
}

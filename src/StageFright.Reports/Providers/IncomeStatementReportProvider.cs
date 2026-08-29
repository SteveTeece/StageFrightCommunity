using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

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
    private readonly ILocalizer _localizer;

    public IncomeStatementReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings, ILocalizer localizer)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
        _localizer = localizer;
    }

    public string ReportId => "income-statement";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_IncomeStatement_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 10;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (from, to) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition
                {
                    Key = "period",
                    Type = ReportFilterType.Select,
                    Label = _localizer.Get<ReportsResource>("Reports_IncomeStatement_PeriodFilterLabel"),
                    Options = ["This FY", "Last FY", "Custom"],
                    OptionLabels =
                    [
                        _localizer.Get<ReportsResource>("Reports_Filter_OptionThisFy"),
                        _localizer.Get<ReportsResource>("Reports_Filter_OptionLastFy"),
                        _localizer.Get<ReportsResource>("Reports_Filter_OptionCustom")
                    ],
                    DefaultValue = "This FY"
                },
                new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_IncomeStatement_DateFromFilterLabel"), DefaultValue = $"{from:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_IncomeStatement_DateToFilterLabel"), DefaultValue = $"{to:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "compare", Type = ReportFilterType.Boolean, Label = _localizer.Get<ReportsResource>("Reports_IncomeStatement_CompareFilterLabel"), DefaultValue = "false" }
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
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Account"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_IncomeStatement_CurrentPeriodColumn"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_IncomeStatement_PriorPeriodColumn"), Alignment = ReportColumnAlignment.Right }
            ]
            :
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Account"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
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
            Title = _localizer.Get<ReportsResource>("Reports_IncomeStatement_Name"),
            SubTitle = compare
                ? _localizer.Get<ReportsResource>("Reports_IncomeStatement_SubTitleCompare",
                    from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy"),
                    priorFrom.ToString("d MMMM yyyy"), priorTo.ToString("d MMMM yyyy"))
                : _localizer.Get<ReportsResource>("Reports_Common_DateRangeSubtitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns = columns,
            Sections =
            [
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Income"), Rows = incomeRows, Subtotal = TotalRow(_localizer.Get<ReportsResource>("Reports_IncomeStatement_TotalIncome"), totalIncome, priorTotalIncome) },
                new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_Section_Expenses"), Rows = expenseRows, Subtotal = TotalRow(_localizer.Get<ReportsResource>("Reports_IncomeStatement_TotalExpenses"), totalExpenses, priorTotalExpenses) }
            ],
            GrandTotal = TotalRow(surplus >= 0 ? _localizer.Get<ReportsResource>("Reports_IncomeStatement_Surplus") : _localizer.Get<ReportsResource>("Reports_IncomeStatement_Deficit"), surplus, priorSurplus)
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

    private static string FormatCurrency(decimal amount) => MoneyFormatter.Format(amount);
}

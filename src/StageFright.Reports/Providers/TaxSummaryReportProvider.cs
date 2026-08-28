using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Tax Summary report on an accruals basis: total taxable sales, total
/// tax-exempt sales, tax collected on sales, tax paid on purchases, and the net amount
/// payable/refundable. Self-explains when sales tax doesn't apply to the organisation.
/// Default range: current tax quarter (aligned to Settings.FinancialYearStartMonth).
/// </summary>
public class TaxSummaryReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;
    private readonly ILocalizer _localizer;

    public TaxSummaryReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings, ILocalizer localizer)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
        _localizer = localizer;
    }

    public string ReportId => "tax-summary";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_TaxSummary_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 60;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (from, to) = GetCurrentQuarterRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_From"), DefaultValue = $"{from:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_To"), DefaultValue = $"{to:yyyy-MM-dd}" }
            ];
        }
    }

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);

        if (settings?.IsTaxApplicable != true)
        {
            return new ReportData
            {
                Title = _localizer.Get<ReportsResource>("Reports_TaxSummary_Name"),
                SubTitle = _localizer.Get<ReportsResource>("Reports_TaxSummary_SubTitleNotApplicable"),
                GeneratedAt = DateTime.UtcNow,
                Columns =
                [
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
                ],
                Sections = []
            };
        }

        var startMonth = settings.FinancialYearStartMonth;
        var (qFrom, qTo) = GetCurrentQuarterRange(DateTime.UtcNow, startMonth);

        var from = DateTime.TryParse(filters.Get("dateFrom"), out var df)
            ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc)
            : qFrom;
        var to = DateTime.TryParse(filters.Get("dateTo"), out var dt)
            ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc)
            : qTo;

        var accountTypes = (await _accounts.GetAllAsync(ct))
            .Concat(await _accounts.GetArchivedAsync(ct))
            .ToDictionary(a => a.Id, a => a.Type);
        var lines = await _gl.GetByDateRangeAsync(from, to, ct);
        var movements = await _gl.GetAccountMovementsAsync(from, to, ct);

        var totalTaxableSales = lines
            .Where(t => t.TaxCode is TaxCode.Taxable or TaxCode.TaxExempt)
            .Where(t => accountTypes.GetValueOrDefault(t.AccountId) == AccountType.Income)
            .Sum(t => t.CreditAmount - t.DebitAmount);
        var totalTaxExemptSales = lines
            .Where(t => t.TaxCode == TaxCode.TaxExempt)
            .Where(t => accountTypes.GetValueOrDefault(t.AccountId) == AccountType.Income)
            .Sum(t => t.CreditAmount - t.DebitAmount);

        var (taxCollectedDebits, taxCollectedCredits) = movements.GetValueOrDefault(SystemAccounts.TaxCollectedId);
        var (taxPaidDebits, taxPaidCredits) = movements.GetValueOrDefault(SystemAccounts.TaxPaidId);

        var taxOnSales = taxCollectedCredits - taxCollectedDebits;   // net CR movement of Tax Collected (2310)
        var taxOnPurchases = taxPaidDebits - taxPaidCredits;         // net DR movement of Tax Paid (2320)
        var totalSales = totalTaxableSales + taxOnSales;
        var net = taxOnSales - taxOnPurchases;

        var rows = new List<ReportRow>
        {
            DescriptionRow(_localizer.Get<ReportsResource>("Reports_TaxSummary_TotalTaxableSales"), totalSales),
            DescriptionRow(_localizer.Get<ReportsResource>("Reports_TaxSummary_TotalTaxExemptSales"), totalTaxExemptSales),
            DescriptionRow(_localizer.Get<ReportsResource>("Reports_TaxSummary_TaxCollectedOnSales"), taxOnSales),
            DescriptionRow(_localizer.Get<ReportsResource>("Reports_TaxSummary_TaxPaidOnPurchases"), taxOnPurchases)
        };

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_TaxSummary_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_TaxSummary_SubTitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = [new ReportSection { Heading = _localizer.Get<ReportsResource>("Reports_TaxSummary_Name"), Rows = rows }],
            GrandTotal = new ReportRow
            {
                Cells =
                [
                    net >= 0
                        ? _localizer.Get<ReportsResource>("Reports_TaxSummary_NetTaxPayable")
                        : _localizer.Get<ReportsResource>("Reports_TaxSummary_NetTaxRefundable"),
                    Math.Abs(net).ToString("F2")
                ],
                IsEmphasized = true
            }
        };
    }

    private static (DateTime From, DateTime To) GetCurrentQuarterRange(DateTime date, int startMonth)
    {
        var (fyFrom, _) = FinancialYearCalculator.GetRange(date, startMonth);
        var monthsElapsed = ((date.Year - fyFrom.Year) * 12) + date.Month - fyFrom.Month;
        var quarterIndex = monthsElapsed / 3;
        var qFrom = fyFrom.AddMonths(quarterIndex * 3);
        var qTo = qFrom.AddMonths(3).AddDays(-1);
        return (qFrom, new DateTime(qTo.Year, qTo.Month, qTo.Day, 23, 59, 59, DateTimeKind.Utc));
    }

    private static ReportRow DescriptionRow(string description, decimal amount) => new()
    {
        Cells = [description, amount.ToString("F2")]
    };
}

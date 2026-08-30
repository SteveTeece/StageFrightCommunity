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
            var (from, to, _) = GetCurrentQuarterRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
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
                BasisOfAccounting = _localizer.Get<ReportsResource>("Reports_Common_BasisOfAccounting"),
                Columns =
                [
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                    new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
                ],
                Sections = []
            };
        }

        var startMonth = settings.FinancialYearStartMonth;
        var startDay = settings.FinancialYearStartDay;
        var (qFrom, qTo, isPartYear) = GetCurrentQuarterRange(DateTime.UtcNow, startMonth, startDay, settings.InceptionDate);

        var hasFrom = DateTime.TryParse(filters.Get("dateFrom"), out var df);
        var hasTo = DateTime.TryParse(filters.Get("dateTo"), out var dt);
        var from = hasFrom ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc) : qFrom;
        var to = hasTo ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc) : qTo;

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
            SubTitle = PartYearSubtitle.Wrap(
                _localizer,
                _localizer.Get<ReportsResource>("Reports_TaxSummary_SubTitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
                isPartYear && !hasFrom && !hasTo),
            GeneratedAt = DateTime.UtcNow,
            BasisOfAccounting = _localizer.Get<ReportsResource>("Reports_Common_BasisOfAccounting"),
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
                    FormatCurrency(Math.Abs(net))
                ],
                IsEmphasized = true
            }
        };
    }

    private static (DateTime From, DateTime To, bool IsPartYear) GetCurrentQuarterRange(
        DateTime date, int startMonth, int startDay = FinancialYearCalculator.DefaultStartDay, DateTime? inceptionDate = null)
    {
        var (fyFrom, _) = FinancialYearCalculator.GetRange(date, startMonth, startDay);
        var monthsElapsed = ((date.Year - fyFrom.Year) * 12) + date.Month - fyFrom.Month;
        var quarterIndex = monthsElapsed / 3;
        var qFrom = fyFrom.AddMonths(quarterIndex * 3);
        var qTo = qFrom.AddMonths(3).AddDays(-1);
        var qToEod = new DateTime(qTo.Year, qTo.Month, qTo.Day, 23, 59, 59, DateTimeKind.Utc);

        // When the organisation was founded partway through this quarter, the quarter opens on the
        // inception date and is reported as a part-year (spec 028, FR-022 / issue #353).
        if (inceptionDate is { } inc)
        {
            var opensAt = inc.Date;
            if (opensAt > qFrom && opensAt <= qToEod)
                return (new DateTime(opensAt.Year, opensAt.Month, opensAt.Day, 0, 0, 0, DateTimeKind.Utc), qToEod, true);
        }

        return (qFrom, qToEod, false);
    }

    private static ReportRow DescriptionRow(string description, decimal amount) => new()
    {
        Cells = [description, FormatCurrency(amount)]
    };

    private static string FormatCurrency(decimal amount) => MoneyFormatter.Format(amount);
}

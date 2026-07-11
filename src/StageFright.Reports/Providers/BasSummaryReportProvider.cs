using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the BAS (Business Activity Statement) Summary report on an accruals
/// basis: G1 total sales, G3 GST-free sales, G11 non-capital purchases, 1A GST on
/// sales, 1B GST on purchases, label 9 net amount payable/refundable. Self-explains
/// when the organisation is not GST registered. Default range: current BAS quarter
/// (aligned to Settings.FinancialYearStartMonth).
/// </summary>
public class BasSummaryReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;

    public BasSummaryReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
    }

    public string ReportId => "bas-summary";
    public string ReportName => "BAS Summary";
    public string ModuleName => "Finance";
    public int DisplayOrder => 60;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (from, to) = GetCurrentQuarterRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From", DefaultValue = $"{from:yyyy-MM-dd}" },
                new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To", DefaultValue = $"{to:yyyy-MM-dd}" }
            ];
        }
    }

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);

        if (settings?.IsGstRegistered != true)
        {
            return new ReportData
            {
                Title = "BAS Summary",
                SubTitle = "The organisation is not registered for GST. Enable GST registration in Settings to generate a BAS.",
                GeneratedAt = DateTime.UtcNow,
                Columns =
                [
                    new ReportColumn { Header = "Label", Alignment = ReportColumnAlignment.Left },
                    new ReportColumn { Header = "Description", Alignment = ReportColumnAlignment.Left },
                    new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
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

        var codedIncomeNet = lines
            .Where(t => t.GstCode is GstCode.Gst or GstCode.GstFree or GstCode.InputTaxed)
            .Where(t => accountTypes.GetValueOrDefault(t.AccountId) == AccountType.Income)
            .Sum(t => t.CreditAmount - t.DebitAmount);
        var codedExpenseNet = lines
            .Where(t => t.GstCode is GstCode.Gst or GstCode.GstFree or GstCode.InputTaxed)
            .Where(t => accountTypes.GetValueOrDefault(t.AccountId) == AccountType.Expense)
            .Sum(t => t.DebitAmount - t.CreditAmount);
        var g3 = lines
            .Where(t => t.GstCode == GstCode.GstFree)
            .Where(t => accountTypes.GetValueOrDefault(t.AccountId) == AccountType.Income)
            .Sum(t => t.CreditAmount - t.DebitAmount);

        var (gstCollectedDebits, gstCollectedCredits) = movements.GetValueOrDefault(SystemAccounts.GstCollectedId);
        var (gstPaidDebits, gstPaidCredits) = movements.GetValueOrDefault(SystemAccounts.GstPaidId);

        var gstOnSales = gstCollectedCredits - gstCollectedDebits;   // 1A: net CR movement of GST Collected (2310)
        var gstOnPurchases = gstPaidDebits - gstPaidCredits;         // 1B: net DR movement of GST Paid (2320)
        var g1 = codedIncomeNet + gstOnSales;
        var g11 = codedExpenseNet + gstPaidDebits;
        var net = gstOnSales - gstOnPurchases;                      // Label 9

        var rows = new List<ReportRow>
        {
            LabelRow("G1", "Total sales (including GST)", g1),
            LabelRow("G3", "GST-free sales", g3),
            LabelRow("G11", "Non-capital purchases (including GST)", g11),
            LabelRow("1A", "GST on sales", gstOnSales),
            LabelRow("1B", "GST on purchases", gstOnPurchases)
        };

        return new ReportData
        {
            Title = "BAS Summary",
            SubTitle = $"Accruals basis — {from:d MMMM yyyy} – {to:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Label", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Description", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
            ],
            Sections = [new ReportSection { Heading = "BAS Labels", Rows = rows }],
            GrandTotal = new ReportRow
            {
                Cells = ["9", $"Net GST {(net >= 0 ? "payable" : "refundable")}", Math.Abs(net).ToString("F2")],
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

    private static ReportRow LabelRow(string label, string description, decimal amount) => new()
    {
        Cells = [label, description, amount.ToString("F2")]
    };
}

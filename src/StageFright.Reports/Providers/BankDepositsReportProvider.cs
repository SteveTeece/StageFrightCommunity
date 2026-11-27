using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Bank Deposits report (issue #399): every bank deposit, newest first, with its
/// date, description, bank account, status and total. Read from the same Banking history the
/// Banking page shows, so deposits recorded before spec 033 are listed too; withdrawals are not.
/// On screen, Reports shows <c>BankDepositsReportView</c>, whose rows open each deposit.
/// </summary>
public class BankDepositsReportProvider : IReportProvider
{
    private readonly IBankingService _banking;
    private readonly ILocalizer _localizer;

    public BankDepositsReportProvider(IBankingService banking, ILocalizer localizer)
    {
        _banking = banking;
        _localizer = localizer;
    }

    public string ReportId => "bank-deposits";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_BankDeposits_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 55;
    public IReadOnlyList<ReportFilterDefinition> Filters => [];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var deposits = (await _banking.GetHistoryAsync(ct))
            .Where(e => e.Type == BankingHistoryEntryType.Deposit)
            .ToList();

        var rows = deposits.Select(d => new ReportRow
        {
            Cells =
            [
                d.Date.ToString("yyyy-MM-dd"),
                d.Description ?? string.Empty,
                d.BankAccountName,
                _localizer.Get<ReportsResource>(d.IsReversed ? "Reports_BankDeposits_Status_Reversed" : "Reports_BankDeposits_Status_Recorded"),
                MoneyFormatter.Format(d.Total)
            ]
        }).ToList();

        return new ReportData
        {
            Title = ReportName,
            SubTitle = deposits.Count == 0
                ? _localizer.Get<ReportsResource>("Reports_BankDeposits_SubTitleNone")
                : _localizer.Get<ReportsResource>("Reports_BankDeposits_SubTitle", DateTime.UtcNow.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Date"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposits_BankAccountColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposits_StatusColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposits_TotalColumn"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = rows.Count == 0 ? [] : [new ReportSection { Rows = rows }]
        };
    }
}

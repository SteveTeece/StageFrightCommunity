using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// The printable report for one bank deposit (issue #399): one row per received item (cheques
/// and other items), a row for the Petty Cash banked, and the deposit total. The subtitle names
/// the description, date, bank account and status, so a reversed deposit prints as reversed.
/// </summary>
public class BankDepositReportBuilder : IBankDepositReportBuilder
{
    private readonly ILocalizer _localizer;

    public BankDepositReportBuilder(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public ReportData Build(DepositDetail deposit)
    {
        var title = _localizer.Get<ReportsResource>("Reports_BankDeposit_Title");

        var rows = deposit.Items.Select(i => new ReportRow
        {
            Cells =
            [
                i.ReceivedDate.ToString("yyyy-MM-dd"),
                PayerText(i),
                i.Reference ?? string.Empty,
                MoneyFormatter.Format(i.Amount)
            ]
        }).ToList();

        if (deposit.CashAmount > 0m)
        {
            rows.Add(new ReportRow
            {
                Cells = [string.Empty, _localizer.Get<ReportsResource>("Reports_BankDeposit_CashRow"), string.Empty, MoneyFormatter.Format(deposit.CashAmount)]
            });
        }

        var status = _localizer.Get<ReportsResource>(deposit.IsReversed ? "Reports_BankDeposits_Status_Reversed" : "Reports_BankDeposits_Status_Recorded");

        return new ReportData
        {
            Title = title,
            SubTitle = _localizer.Get<ReportsResource>(
                "Reports_BankDeposit_SubTitle",
                string.IsNullOrWhiteSpace(deposit.Description) ? title : deposit.Description,
                deposit.Date.ToString("d MMMM yyyy"),
                deposit.BankAccountName,
                status),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposit_ReceivedColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposit_PayerColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankDeposit_ReferenceColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = [new ReportSection { Rows = rows }],
            GrandTotal = new ReportRow
            {
                Cells = [string.Empty, _localizer.Get<ReportsResource>("Reports_BankDeposit_Total"), string.Empty, MoneyFormatter.Format(deposit.Total)],
                IsEmphasized = true
            }
        };
    }

    private string PayerText(AwaitingDepositItem item) =>
        item.Source == ReceivedItemSource.OpeningBalance && string.IsNullOrEmpty(item.PayerName)
            ? _localizer.Get<ReportsResource>("Reports_BankDeposit_OpeningBalancePayer")
            : item.PayerName ?? string.Empty;
}

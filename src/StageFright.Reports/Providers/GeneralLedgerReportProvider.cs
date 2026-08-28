using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the General Ledger detail report: one section per matching account,
/// showing its opening balance (inception-to-date balance immediately before the
/// range), each dated GL line with a running balance, and a closing balance. An
/// optional account filter (number or name, blank = all) narrows the report; accounts
/// with no opening balance and no movement in the range are omitted.
/// </summary>
public class GeneralLedgerReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ILocalizer _localizer;

    public GeneralLedgerReportProvider(IGLRepository gl, IAccountRepository accounts, ILocalizer localizer)
    {
        _gl = gl;
        _accounts = accounts;
        _localizer = localizer;
    }

    public string ReportId => "general-ledger";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_GeneralLedger_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 35;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "account", Type = ReportFilterType.Text, Label = _localizer.Get<ReportsResource>("Reports_Filter_AccountNumberOrName"), DefaultValue = "" },
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_From"), DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_To"), DefaultValue = $"{DateTime.UtcNow.Year}-12-31" }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = ParseDateRange(filters);
        var accountFilter = filters.Get("account")?.Trim();

        var matchingAccounts = ((await _accounts.GetAllAsync(ct)).Concat(await _accounts.GetArchivedAsync(ct)))
            .Where(a => string.IsNullOrEmpty(accountFilter)
                || a.AccountNumber.Equals(accountFilter, StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains(accountFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.AccountNumber)
            .ToList();

        var allTxns = await _gl.GetByDateRangeAsync(from, to, ct);
        var txnsByAccount = allTxns.GroupBy(t => t.AccountId).ToDictionary(g => g.Key, g => g.ToList());

        var sections = new List<ReportSection>();
        foreach (var account in matchingAccounts)
        {
            var opening = await _gl.GetAccountBalanceAsync(account.Id, from.AddTicks(-1), ct);
            var lines = txnsByAccount.GetValueOrDefault(account.Id, [])
                .OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToList();

            if (opening == 0m && lines.Count == 0)
                continue;

            var rows = new List<ReportRow>
            {
                new() { Cells = ["", _localizer.Get<ReportsResource>("Reports_Common_OpeningBalance"), "", "", FormatCurrency(opening)] }
            };

            var runningBalance = opening;
            foreach (var line in lines)
            {
                runningBalance += line.DebitAmount - line.CreditAmount;
                rows.Add(new ReportRow
                {
                    Cells =
                    [
                        line.Date.ToString("yyyy-MM-dd"),
                        line.Description ?? string.Empty,
                        line.DebitAmount != 0m ? FormatCurrency(line.DebitAmount) : string.Empty,
                        line.CreditAmount != 0m ? FormatCurrency(line.CreditAmount) : string.Empty,
                        FormatCurrency(runningBalance)
                    ]
                });
            }

            sections.Add(new ReportSection
            {
                Heading = $"{account.Name} ({account.AccountNumber})",
                Rows = rows,
                Subtotal = new ReportRow { Cells = ["", _localizer.Get<ReportsResource>("Reports_Common_ClosingBalance"), "", "", FormatCurrency(runningBalance)], IsEmphasized = true }
            });
        }

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_GeneralLedger_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_Common_DateRangeSubtitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Date"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Debit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Credit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Balance"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections
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

using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Account Register report showing all GL transactions in chronological order
/// with a running balance column. Supports date-range filter.
/// </summary>
public class AccountRegisterReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ILocalizer _localizer;

    public AccountRegisterReportProvider(IGLRepository gl, IAccountRepository accounts, ILocalizer localizer)
    {
        _gl = gl;
        _accounts = accounts;
        _localizer = localizer;
    }

    public string ReportId => "account-register";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_AccountRegister_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 30;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_From"), DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_To"), DefaultValue = $"{DateTime.UtcNow.Year}-12-31" }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = ParseDateRange(filters);
        // Include archived accounts so historical transactions still resolve a name.
        var allAccounts = (await _accounts.GetAllAsync(ct))
            .Concat(await _accounts.GetArchivedAsync(ct))
            .ToList();
        var catById = allAccounts.ToDictionary(c => c.Id);

        var transactions = await _gl.GetByDateRangeAsync(from, to, ct);
        var ordered = transactions.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToList();

        decimal runningBalance = 0m;
        var rows = new List<ReportRow>();
        foreach (var txn in ordered)
        {
            runningBalance += txn.CreditAmount - txn.DebitAmount;
            var accountName = catById.TryGetValue(txn.AccountId, out var cat) ? cat.Name : txn.GLAccount;

            rows.Add(new ReportRow
            {
                Cells =
                [
                    txn.Date.ToString("yyyy-MM-dd"),
                    txn.Description ?? accountName,
                    accountName,
                    txn.DebitAmount > 0 ? FormatCurrency(txn.DebitAmount) : string.Empty,
                    txn.CreditAmount > 0 ? FormatCurrency(txn.CreditAmount) : string.Empty,
                    FormatCurrency(runningBalance)
                ]
            });
        }

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_AccountRegister_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_Common_DateRangeSubtitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            BasisOfAccounting = _localizer.Get<ReportsResource>("Reports_Common_BasisOfAccounting"),
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Date"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Account"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Debit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Credit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_AccountRegister_RunningBalanceColumn"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = [new ReportSection { Rows = rows }]
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

    private static string FormatCurrency(decimal amount) => MoneyFormatter.Format(amount);
}

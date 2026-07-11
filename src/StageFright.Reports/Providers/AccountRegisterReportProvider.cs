using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Account Register report showing all GL transactions in chronological order
/// with a running balance column. Supports date-range filter.
/// </summary>
public class AccountRegisterReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;

    public AccountRegisterReportProvider(IGLRepository gl, IAccountRepository accounts)
    {
        _gl = gl;
        _accounts = accounts;
    }

    public string ReportId => "account-register";
    public string ReportName => "Account Register";
    public string ModuleName => "Finance";
    public int DisplayOrder => 30;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = "From", DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = "To", DefaultValue = $"{DateTime.UtcNow.Year}-12-31" }
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
            Title = "Account Register",
            SubTitle = $"{from:d MMMM yyyy} – {to:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Date", Alignment = ReportColumnAlignment.Left, Format = ReportColumnFormat.Date },
                new ReportColumn { Header = "Description", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Debit", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency },
                new ReportColumn { Header = "Credit", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency },
                new ReportColumn { Header = "Running Balance", Alignment = ReportColumnAlignment.Right, Format = ReportColumnFormat.Currency }
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

    private static string FormatCurrency(decimal amount) => amount.ToString("F2");
}

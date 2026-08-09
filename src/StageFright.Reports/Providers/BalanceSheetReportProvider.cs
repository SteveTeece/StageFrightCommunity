using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Statement of Financial Position (Balance Sheet). Asset, Liability,
/// and Equity sections derive from inception-to-date AccountId balances as at a single
/// date. Accumulated Surplus (3200) is never posted to directly — there is no year-end
/// closing process — so its value is computed as inception-to-date net income (Income
/// minus Expenses) and substituted for the account's own (always-zero) GL balance.
/// This is what makes Assets = Liabilities + Equity hold.
/// </summary>
public class BalanceSheetReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;

    public BalanceSheetReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
    }

    public string ReportId => "balance-sheet";
    public string ReportName => "Statement of Financial Position";
    public string ModuleName => "Finance";
    public int DisplayOrder => 25;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (_, fyEnd) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "asAt", Type = ReportFilterType.Date, Label = "As at", DefaultValue = $"{fyEnd:yyyy-MM-dd}" }
            ];
        }
    }

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var asAt = await ParseAsAtAsync(filters, ct);

        var allAccounts = (await _accounts.GetAllAsync(ct))
            .Concat(await _accounts.GetArchivedAsync(ct))
            .ToList();

        var (assetRows, totalAssets) = await SectionAsync(allAccounts, AccountType.Asset, creditNormal: false, asAt, ct);
        var (liabilityRows, totalLiabilities) = await SectionAsync(allAccounts, AccountType.Liability, creditNormal: true, asAt, ct);
        var (equityRows, totalEquity) = await SectionAsync(allAccounts, AccountType.Equity, creditNormal: true, asAt, ct);

        var accumulatedSurplus = await ComputeAccumulatedSurplusAsync(allAccounts, asAt, ct);
        equityRows.Add(new ReportRow { Cells = ["Accumulated Surplus", FormatCurrency(accumulatedSurplus)] });
        totalEquity += accumulatedSurplus;

        return new ReportData
        {
            Title = "Statement of Financial Position",
            SubTitle = $"As at {asAt:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right }
            ],
            Sections =
            [
                new ReportSection
                {
                    Heading = "Assets",
                    Rows = assetRows,
                    Subtotal = new ReportRow { Cells = ["Total Assets", FormatCurrency(totalAssets)], IsEmphasized = true }
                },
                new ReportSection
                {
                    Heading = "Liabilities",
                    Rows = liabilityRows,
                    Subtotal = new ReportRow { Cells = ["Total Liabilities", FormatCurrency(totalLiabilities)], IsEmphasized = true }
                },
                new ReportSection
                {
                    Heading = "Equity",
                    Rows = equityRows,
                    Subtotal = new ReportRow { Cells = ["Total Equity", FormatCurrency(totalEquity)], IsEmphasized = true }
                }
            ],
            GrandTotal = new ReportRow
            {
                Cells = ["Total Liabilities + Equity", FormatCurrency(totalLiabilities + totalEquity)],
                IsEmphasized = true
            }
        };
    }

    /// <summary>
    /// Builds the rows for one balance-sheet section. Archived accounts with a zero
    /// balance as at the report date are omitted; active accounts always show.
    /// </summary>
    private async Task<(List<ReportRow> Rows, decimal Total)> SectionAsync(
        IReadOnlyList<Account> allAccounts, AccountType type, bool creditNormal, DateTime asAt, CancellationToken ct)
    {
        var rows = new List<ReportRow>();
        decimal total = 0m;

        foreach (var account in allAccounts
            .Where(a => a.Type == type && a.Id != SystemAccounts.AccumulatedSurplusId)
            .OrderBy(a => a.AccountNumber))
        {
            var netDebit = await _gl.GetAccountBalanceAsync(account.Id, asAt, ct);
            var displayed = creditNormal ? -netDebit : netDebit;

            if (account.IsDeleted && displayed == 0m)
                continue;

            rows.Add(new ReportRow { Cells = [$"{account.Name} ({account.AccountNumber})", FormatCurrency(displayed)] });
            total += displayed;
        }

        return (rows, total);
    }

    /// <summary>Net income (credits − debits for Income, debits − credits negated for Expense) since inception, up to <paramref name="asAt"/>.</summary>
    private async Task<decimal> ComputeAccumulatedSurplusAsync(IReadOnlyList<Account> allAccounts, DateTime asAt, CancellationToken ct)
    {
        decimal netIncome = 0m;
        foreach (var account in allAccounts.Where(a => a.Type is AccountType.Income or AccountType.Expense))
            netIncome += -(await _gl.GetAccountBalanceAsync(account.Id, asAt, ct));

        return netIncome;
    }

    private async Task<DateTime> ParseAsAtAsync(ReportFilterValues filters, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var startMonth = settings?.FinancialYearStartMonth ?? FinancialYearCalculator.DefaultStartMonth;
        var (_, fyEnd) = FinancialYearCalculator.GetRange(DateTime.UtcNow, startMonth);

        return DateTime.TryParse(filters.Get("asAt"), out var d)
            ? new DateTime(d.Year, d.Month, d.Day, 23, 59, 59, DateTimeKind.Utc)
            : fyEnd;
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("F2");
}

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
/// Generates the Statement of Financial Position (Balance Sheet). Asset, Liability,
/// and Equity sections derive from inception-to-date AccountId balances as at a single
/// date. Accumulated Surplus (3200) is never posted to directly — there is no year-end
/// closing process — so its value is computed as inception-to-date net income (Income
/// minus Expenses) and substituted for the account's own (always-zero) GL balance.
/// This is what makes Assets = Liabilities + Equity hold. If a corrupted ledger breaks
/// that identity, an explicit out-of-balance line is appended and no clean statement is
/// produced (FR-010).
/// </summary>
public class BalanceSheetReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IAccountRepository _accounts;
    private readonly ISettingsRepository _settings;
    private readonly ILocalizer _localizer;

    public BalanceSheetReportProvider(IGLRepository gl, IAccountRepository accounts, ISettingsRepository settings, ILocalizer localizer)
    {
        _gl = gl;
        _accounts = accounts;
        _settings = settings;
        _localizer = localizer;
    }

    public string ReportId => "balance-sheet";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_BalanceSheet_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 25;

    public IReadOnlyList<ReportFilterDefinition> Filters
    {
        get
        {
            var (_, fyEnd) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
            return
            [
                new ReportFilterDefinition { Key = "asAt", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_BalanceSheet_AsAtFilterLabel"), DefaultValue = $"{fyEnd:yyyy-MM-dd}" }
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
        equityRows.Add(new ReportRow { Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_AccumulatedSurplus"), FormatCurrency(accumulatedSurplus)] });
        totalEquity += accumulatedSurplus;

        var sections = new List<ReportSection>
        {
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_Section_Assets"),
                Rows = assetRows,
                Subtotal = new ReportRow { Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_TotalAssets"), FormatCurrency(totalAssets)], IsEmphasized = true }
            },
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_Section_Liabilities"),
                Rows = liabilityRows,
                Subtotal = new ReportRow { Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_TotalLiabilities"), FormatCurrency(totalLiabilities)], IsEmphasized = true }
            },
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_Section_Equity"),
                Rows = equityRows,
                Subtotal = new ReportRow { Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_TotalEquity"), FormatCurrency(totalEquity)], IsEmphasized = true }
            }
        };

        // FR-010: the Balance Sheet balances by construction (Accumulated Surplus is computed net
        // income), so a non-zero difference here means the ledger itself is corrupt. Append an
        // explicit out-of-balance line rather than presenting a clean statement.
        var outOfBalance = totalAssets - (totalLiabilities + totalEquity);
        if (outOfBalance != 0m)
        {
            sections.Add(new ReportSection
            {
                Rows =
                [
                    new ReportRow
                    {
                        Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_OutOfBalance"), FormatCurrency(outOfBalance)],
                        IsEmphasized = true
                    }
                ]
            });
        }

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_BalanceSheet_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_BalanceSheet_SubTitle", asAt.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Account"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Amount"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections,
            GrandTotal = new ReportRow
            {
                Cells = [_localizer.Get<ReportsResource>("Reports_BalanceSheet_TotalLiabilitiesPlusEquity"), FormatCurrency(totalLiabilities + totalEquity)],
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

    private static string FormatCurrency(decimal amount) => MoneyFormatter.Format(amount);
}

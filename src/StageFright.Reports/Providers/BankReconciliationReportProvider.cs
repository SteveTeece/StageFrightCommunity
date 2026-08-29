using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Bank Reconciliation report in the conventional adjusted-balance form
/// (spec 028 US5, FR-013…FR-015): for each bank/cash account's most recent reconciliation,
/// at its statement date — balance per bank statement, adjusted by outstanding deposits
/// (added) and outstanding payments (deducted) to an adjusted bank balance, reconciled to
/// the balance per the general ledger as at the statement date. Both balances are shown and
/// the reconciled residual demonstrates they agree. Outstanding items are carried into the
/// arithmetic, not merely listed. An optional account filter narrows the report to one
/// account (matched by number or name).
/// </summary>
public class BankReconciliationReportProvider : IReportProvider
{
    private readonly IBankReconciliationRepository _reconciliations;
    private readonly IAccountRepository _accounts;
    private readonly IGLRepository _gl;
    private readonly ILocalizer _localizer;

    public BankReconciliationReportProvider(
        IBankReconciliationRepository reconciliations,
        IAccountRepository accounts,
        IGLRepository gl,
        ILocalizer localizer)
    {
        _reconciliations = reconciliations;
        _accounts = accounts;
        _gl = gl;
        _localizer = localizer;
    }

    public string ReportId => "bank-reconciliation";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_BankReconciliation_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 50;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition
        {
            Key = "account",
            Type = ReportFilterType.Text,
            Label = _localizer.Get<ReportsResource>("Reports_Filter_AccountNumberOrName"),
            DefaultValue = ""
        }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var accountFilter = filters.Get("account")?.Trim();

        var bankAccounts = (await _accounts.GetAllAsync(ct))
            .Where(a => a.IsBankAccount)
            .Where(a => string.IsNullOrEmpty(accountFilter)
                || a.AccountNumber.Equals(accountFilter, StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains(accountFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.AccountNumber)
            .ToList();

        var sections = new List<ReportSection>();
        foreach (var account in bankAccounts)
        {
            var history = await _reconciliations.GetByAccountAsync(account.Id, ct);
            var latest = history.FirstOrDefault();
            if (latest is null)
                continue;

            sections.AddRange(await BuildAccountSectionsAsync(account, latest.Id, ct));
        }

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_BankReconciliation_Name"),
            SubTitle = sections.Count == 0
                ? _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubTitleNone")
                : _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubTitle", DateTime.UtcNow.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            BasisOfAccounting = _localizer.Get<ReportsResource>("Reports_Common_BasisOfAccounting"),
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Date"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankReconciliation_DepositColumn"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_BankReconciliation_PaymentColumn"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections
        };
    }

    private async Task<IReadOnlyList<ReportSection>> BuildAccountSectionsAsync(
        Account account, Guid reconciliationId, CancellationToken ct)
    {
        // Re-fetch by id to load the cleared lines with their transactions.
        var reconciliation = await _reconciliations.GetByIdAsync(reconciliationId, ct);
        if (reconciliation is null)
            return Array.Empty<ReportSection>();

        var header = _localizer.Get<ReportsResource>(
            "Reports_BankReconciliation_AccountHeader",
            account.Name,
            account.AccountNumber,
            reconciliation.StatementDate.ToString("d MMMM yyyy"),
            _localizer.Enum(reconciliation.Status));

        // Outstanding = GL transactions on this account, up to the statement date, not yet
        // cleared by any non-deleted reconciliation. Bank-account debits are deposits in
        // transit; credits are unpresented payments.
        var outstanding = await _gl.GetUnreconciledByAccountAsync(account.Id, reconciliation.StatementDate, ct);
        var outstandingDeposits = outstanding.Where(t => t.DebitAmount != 0m).ToList();
        var outstandingPayments = outstanding.Where(t => t.CreditAmount != 0m).ToList();

        var depositsTotal = outstandingDeposits.Sum(t => t.DebitAmount);
        var paymentsTotal = outstandingPayments.Sum(t => t.CreditAmount);

        var statementBalance = reconciliation.StatementClosingBalance;
        var adjustedBankBalance = statementBalance + depositsTotal - paymentsTotal;
        var ledgerBalance = await _gl.GetAccountBalanceAsync(account.Id, reconciliation.StatementDate, ct);
        var residual = adjustedBankBalance - ledgerBalance;

        var rows = new List<ReportRow>
        {
            BalanceRow("Reports_BankReconciliation_BalancePerBankStatement", statementBalance)
        };
        rows.AddRange(outstandingDeposits.Select(DepositDetailRow));
        rows.Add(DepositSubtotalRow("Reports_BankReconciliation_AddOutstandingDeposits", depositsTotal));
        rows.AddRange(outstandingPayments.Select(PaymentDetailRow));
        rows.Add(BalanceRow("Reports_BankReconciliation_LessOutstandingPayments", paymentsTotal));
        rows.Add(BalanceRow("Reports_BankReconciliation_AdjustedBankBalance", adjustedBankBalance));
        rows.Add(BalanceRow("Reports_BankReconciliation_BalancePerGeneralLedger", ledgerBalance));
        rows.Add(BalanceRow("Reports_BankReconciliation_Reconciled", residual));

        return [new ReportSection { Heading = header, Rows = rows }];
    }

    // Label/amount summary line — amount in the Payment (last) column, matching this report's
    // existing label-row convention.
    private ReportRow BalanceRow(string labelKey, decimal amount) => new()
    {
        Cells = [string.Empty, _localizer.Get<ReportsResource>(labelKey), string.Empty, MoneyFormatter.Format(amount)],
        IsEmphasized = true
    };

    // Outstanding-deposit subtotal — amount in the Deposit column, above the itemised deposits.
    private ReportRow DepositSubtotalRow(string labelKey, decimal amount) => new()
    {
        Cells = [string.Empty, _localizer.Get<ReportsResource>(labelKey), MoneyFormatter.Format(amount), string.Empty],
        IsEmphasized = true
    };

    private static ReportRow DepositDetailRow(Transaction txn) => new()
    {
        Cells =
        [
            txn.Date.ToString("yyyy-MM-dd"),
            txn.Description ?? string.Empty,
            MoneyFormatter.Format(txn.DebitAmount),
            string.Empty
        ]
    };

    private static ReportRow PaymentDetailRow(Transaction txn) => new()
    {
        Cells =
        [
            txn.Date.ToString("yyyy-MM-dd"),
            txn.Description ?? string.Empty,
            string.Empty,
            MoneyFormatter.Format(txn.CreditAmount)
        ]
    };
}

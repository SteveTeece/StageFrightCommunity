using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Bank Reconciliation report: for each bank/cash account's most recent
/// reconciliation — cleared deposits, cleared payments, unpresented items up to the
/// statement date, and a summary with the difference. An optional account filter
/// narrows the report to one account (matched by number or name).
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

        var cleared = reconciliation.Lines
            .Where(l => l.Transaction is not null)
            .Select(l => l.Transaction!)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var deposits = cleared.Where(t => t.DebitAmount != 0m).ToList();
        var payments = cleared.Where(t => t.CreditAmount != 0m).ToList();
        var unpresented = await _gl.GetUnreconciledByAccountAsync(account.Id, reconciliation.StatementDate, ct);

        var clearedTotal = cleared.Sum(t => t.DebitAmount) - cleared.Sum(t => t.CreditAmount);
        var difference = reconciliation.StatementClosingBalance - (reconciliation.OpeningBalance + clearedTotal);

        return
        [
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubHeaderClearedDeposits", header),
                Rows = deposits.Select(t => TransactionRow(t)).ToList(),
                Subtotal = SummaryRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_TotalClearedDeposits"), deposits.Sum(t => t.DebitAmount), depositColumn: true)
            },
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubHeaderClearedPayments", header),
                Rows = payments.Select(t => TransactionRow(t)).ToList(),
                Subtotal = SummaryRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_TotalClearedPayments"), payments.Sum(t => t.CreditAmount), depositColumn: false)
            },
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubHeaderUnpresentedItems", header),
                Rows = unpresented.Select(t => TransactionRow(t)).ToList()
            },
            new ReportSection
            {
                Heading = _localizer.Get<ReportsResource>("Reports_BankReconciliation_SubHeaderSummary", header),
                Rows =
                [
                    LabelAmountRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_StatementClosingBalance"), reconciliation.StatementClosingBalance),
                    LabelAmountRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_OpeningBalance"), reconciliation.OpeningBalance),
                    LabelAmountRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_ClearedMovement"), clearedTotal),
                    LabelAmountRow(_localizer.Get<ReportsResource>("Reports_BankReconciliation_Difference"), difference)
                ]
            }
        ];
    }

    private static ReportRow TransactionRow(Transaction txn) => new()
    {
        Cells =
        [
            txn.Date.ToString("yyyy-MM-dd"),
            txn.Description ?? string.Empty,
            txn.DebitAmount != 0m ? txn.DebitAmount.ToString("F2") : string.Empty,
            txn.CreditAmount != 0m ? txn.CreditAmount.ToString("F2") : string.Empty
        ]
    };

    private static ReportRow SummaryRow(string label, decimal amount, bool depositColumn) => new()
    {
        Cells =
        [
            string.Empty,
            label,
            depositColumn ? amount.ToString("F2") : string.Empty,
            depositColumn ? string.Empty : amount.ToString("F2")
        ]
    };

    private static ReportRow LabelAmountRow(string label, decimal amount) => new()
    {
        Cells = [string.Empty, label, string.Empty, amount.ToString("F2")]
    };
}

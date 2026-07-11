using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// Header for a manual bank reconciliation of one bank/cash account against a paper
/// statement. Drafts are editable and soft-deletable; finalised reconciliations are
/// immutable and undeletable (enforced via ReconciliationException).
/// CSV statement import seam: a future StatementLine entity (imported statement rows,
/// FK to this header) slots in beside ReconciliationLine with zero rework — cleared
/// GL transactions and imported statement rows remain independent child collections.
/// </summary>
public class BankReconciliation
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The bank/cash account being reconciled. Must have IsBankAccount=true.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Closing date of the bank statement being reconciled.</summary>
    public DateTime StatementDate { get; set; }

    /// <summary>Closing balance printed on the bank statement.</summary>
    public decimal StatementClosingBalance { get; set; }

    /// <summary>
    /// Opening balance snapshot: the previous finalised reconciliation's closing
    /// balance for the same account, or 0 for the first reconciliation.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Draft (editable) or Finalised (immutable).</summary>
    public ReconciliationStatus Status { get; set; }

    /// <summary>UTC timestamp when the reconciliation was finalised. Null while draft.</summary>
    public DateTime? FinalisedAt { get; set; }

    /// <summary>Optional free-form notes.</summary>
    public string? Notes { get; set; }

    // --- Soft-delete fields (drafts only; finalised reconciliations are undeletable) ---

    /// <summary>True when a draft reconciliation has been discarded.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the draft was discarded.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that discarded the draft.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The account being reconciled.</summary>
    public Account? Account { get; set; }

    /// <summary>GL transactions ticked off (cleared) against the statement.</summary>
    public List<ReconciliationLine> Lines { get; set; } = new();
}

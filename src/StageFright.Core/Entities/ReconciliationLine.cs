namespace StageFright.Core.Entities;

/// <summary>
/// Join row marking one GL transaction as cleared within one bank reconciliation.
/// Unique on (ReconciliationId, TransactionId); the service additionally enforces that
/// a transaction is cleared by at most one non-deleted reconciliation.
/// The GL Transaction row itself is never modified.
/// </summary>
public class ReconciliationLine
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The reconciliation this cleared line belongs to.</summary>
    public Guid ReconciliationId { get; set; }

    /// <summary>The GL transaction ticked off against the statement.</summary>
    public Guid TransactionId { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The parent reconciliation.</summary>
    public BankReconciliation? Reconciliation { get; set; }

    /// <summary>The cleared GL transaction.</summary>
    public Transaction? Transaction { get; set; }
}

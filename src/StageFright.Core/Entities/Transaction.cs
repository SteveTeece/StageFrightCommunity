namespace StageFright.Core.Entities;

/// <summary>
/// A General Ledger entry. Immutable — NO soft-delete fields per Constitution §3.4.
/// Every financial operation creates exactly two rows (debit + credit) with equal amounts,
/// committed atomically. GL-balance verification (Σdebits = Σcredits within 0.01) runs
/// before every commit; GLBalanceException triggers full rollback on failure.
/// This table is the single source of truth for all balances and reports.
/// </summary>
public class Transaction
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>UTC date of the transaction (matches the source operation date).</summary>
    public DateTime Date { get; set; }

    /// <summary>FK to the chart-of-accounts entry this row is posted to.</summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Debit amount. Exactly one of DebitAmount / CreditAmount is non-zero per row.
    /// Precision: decimal(18,2).
    /// </summary>
    public decimal DebitAmount { get; set; }

    /// <summary>
    /// Credit amount. Exactly one of DebitAmount / CreditAmount is non-zero per row.
    /// Precision: decimal(18,2).
    /// </summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Account number stored denormalized at posting time. Historical rows keep whatever
    /// number was current when they were posted (legacy "0100"/"0101"/"10xx"/"20xx"/"9900"
    /// or current "1100"/"1200"/"4xxx"/"6xxx" ranges) — this is a snapshot and is NEVER
    /// updated; all aggregation keys on <see cref="AccountId"/> instead.
    /// </summary>
    public string GLAccount { get; set; } = string.Empty;

    /// <summary>FK to the member when the transaction relates to a member's account. Nullable.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>FK to the payment that generated this transaction. Nullable.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>FK to the fee that generated or was settled by this transaction. Nullable.</summary>
    public Guid? FeeId { get; set; }

    /// <summary>
    /// Optional description. Reversing entries MUST state what was reversed and why
    /// (Constitution §3.6).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The account this transaction is posted to.</summary>
    public Account Account { get; set; } = null!;

    /// <summary>The member this transaction relates to, if any.</summary>
    public Member? Member { get; set; }

    /// <summary>The payment that generated this transaction, if any.</summary>
    public Payment? Payment { get; set; }

    /// <summary>The fee associated with this transaction, if any.</summary>
    public Fee? Fee { get; set; }
}

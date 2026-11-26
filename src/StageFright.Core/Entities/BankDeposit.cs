namespace StageFright.Core.Entities;

/// <summary>
/// A deposit of waiting cheques/other receipts and/or Petty Cash into a bank account (spec 033).
/// A financial record: it carries no soft-delete fields and is never deleted — like <see cref="Fee"/>,
/// <see cref="Payment"/> and <see cref="Transaction"/>. A mistaken deposit is undone by posting a
/// reversing journal and setting <see cref="ReversalJournalEntryId"/> and <see cref="ReversedAt"/> once.
/// Status is derived: Recorded while <see cref="ReversalJournalEntryId"/> is null, Reversed afterwards.
/// </summary>
public class BankDeposit
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Deposit date. Not before any included item's date; inside an open period.</summary>
    public DateTime Date { get; set; }

    /// <summary>The bank account receiving the deposit. Never Petty Cash.</summary>
    public Guid BankAccountId { get; set; }

    /// <summary>Petty Cash banked in this deposit. Zero or more, up to the cash available.</summary>
    public decimal CashAmount { get; set; }

    /// <summary>Sum of the included received items' amounts.</summary>
    public decimal ChequeTotal { get; set; }

    /// <summary>The <c>BankDeposit</c> journal entry that posted this deposit. Unique.</summary>
    public Guid JournalEntryId { get; set; }

    /// <summary>The <c>BankDepositReversal</c> journal entry, once reversed. Unique when set; set once.</summary>
    public Guid? ReversalJournalEntryId { get; set; }

    /// <summary>UTC timestamp of the reversal. Set together with <see cref="ReversalJournalEntryId"/>.</summary>
    public DateTime? ReversedAt { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Total banked: <see cref="CashAmount"/> plus <see cref="ChequeTotal"/>. Derived, not stored.</summary>
    public decimal Total => CashAmount + ChequeTotal;

    // --- Navigation ---

    /// <summary>The bank account that received the deposit.</summary>
    public Account? BankAccount { get; set; }

    /// <summary>The journal entry that posted the deposit.</summary>
    public JournalEntry? JournalEntry { get; set; }

    /// <summary>The journal entry that reversed the deposit, if any.</summary>
    public JournalEntry? ReversalJournalEntry { get; set; }

    /// <summary>The received items banked in this deposit.</summary>
    public List<BankDepositLine> Lines { get; set; } = new();
}

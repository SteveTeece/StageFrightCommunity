using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// A cheque or "other" receipt waiting to be banked, or the single opening-balance
/// amount on Undeposited Funds (spec 033). Exactly one of <see cref="PaymentId"/> or
/// <see cref="JournalEntryId"/> is set. Financial record: it carries no soft-delete
/// fields (financial exemption, like Fee/Payment/Transaction) and is never deleted.
/// Whether an item is still awaiting deposit is derived from its deposit lines, never stored.
/// </summary>
public class ReceivedItem
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The receipt's posting date.</summary>
    public DateTime Date { get; set; }

    /// <summary>Gross amount debited to Undeposited Funds for this receipt. Greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>How the receipt was made: <see cref="PaymentMethod.Check"/> or <see cref="PaymentMethod.Other"/>.</summary>
    public PaymentMethod Method { get; set; }

    /// <summary>Optional cheque number or note (max 50, trimmed, null when blank). Set once.</summary>
    public string? Reference { get; set; }

    /// <summary>The member payment this item came from. Null for an income or opening-balance item.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>The journal entry this item came from (income or opening balance). Null for a member payment.</summary>
    public Guid? JournalEntryId { get; set; }

    /// <summary>UTC timestamp when the item was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The member payment this item came from, if any.</summary>
    public Payment? Payment { get; set; }

    /// <summary>The income or opening-balance journal entry this item came from, if any.</summary>
    public JournalEntry? JournalEntry { get; set; }
}

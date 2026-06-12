using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// An immutable payment record. NO soft-delete fields per Constitution §3.4.
/// Only the Notes field may be edited after creation; all other fields are locked.
/// The repository enforces immutability: UpdateNotesAsync is the sole mutator.
/// UpdatedAt differs from CreatedAt only when Notes has been edited.
/// </summary>
public class Payment
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the member who made this payment.</summary>
    public Guid MemberId { get; set; }

    /// <summary>UTC date the payment was received. Immutable.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount paid. Immutable. Precision: decimal(18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary>Method used to tender the payment. Defaults to Cash. Immutable.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>
    /// Reporting classification of the payment (Annual, Attendance, Other).
    /// Distinct from GL category. Immutable.
    /// </summary>
    public PaymentType PaymentType { get; set; }

    /// <summary>
    /// Free-text notes. The ONLY editable field after creation.
    /// Edits are audited with old and new values.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent update.
    /// Changes ONLY when Notes is edited; UpdatedAt ≠ CreatedAt implies Notes was modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The member who made this payment.</summary>
    public Member Member { get; set; } = null!;
}

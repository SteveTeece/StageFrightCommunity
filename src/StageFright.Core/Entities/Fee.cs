using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// An immutable financial obligation record. NO soft-delete fields per Constitution §3.4.
/// Corrections are made via GL reversing pairs only — Fee records are never updated or deleted.
/// Outstanding balance is derived entirely from GL transactions, never from this record.
/// </summary>
public class Fee
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the member who owes this fee.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Classification of the fee (Annual, Attendance, Other). Immutable.</summary>
    public FeeType FeeType { get; set; }

    /// <summary>Amount of the fee in the local currency. Immutable. Precision: decimal(18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date the fee applies to. Annual = Jan 1 of the year;
    /// Attendance = rehearsal date; Other = date specified on creation. Immutable.
    /// </summary>
    public DateTime FeeDate { get; set; }

    /// <summary>
    /// Date by which payment is expected. Annual = Dec 31 of the year;
    /// Attendance = rehearsal date; Other = CreatedAt + 30 days unless specified. Immutable.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Metadata flag set at creation. Attendance fees default true (paid immediately unless
    /// "Mark as unpaid" is checked). Annual fees default false. Immutable.
    /// The GL is the source of truth for actual payment status.
    /// </summary>
    public bool PaidAtCreation { get; set; }

    /// <summary>
    /// FK to the rehearsal for attendance fees. Enables idempotency check
    /// (one attendance fee per member per rehearsal). Null for non-attendance fees.
    /// </summary>
    public Guid? RehearsalId { get; set; }

    /// <summary>UTC timestamp when the record was created. Used as FIFO tiebreaker.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The member who owes this fee.</summary>
    public Member Member { get; set; } = null!;

    /// <summary>The rehearsal linked to this attendance fee, if applicable.</summary>
    public Rehearsal? Rehearsal { get; set; }
}

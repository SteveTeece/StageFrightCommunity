namespace StageFright.Core.Entities;

/// <summary>
/// Records whether a specific member attended a specific rehearsal.
/// Immutable after batch save — no edit or delete UI exists.
/// The combination (RehearsalId, MemberId) is unique.
/// Soft-delete fields are present per Constitution §3.4 but are never set by any MVP workflow.
/// Corrections are made via manual GL reversals only.
/// </summary>
public class AttendanceRecord
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the rehearsal this record belongs to.</summary>
    public Guid RehearsalId { get; set; }

    /// <summary>FK to the member this attendance record concerns.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Whether the member attended the rehearsal.</summary>
    public bool Attended { get; set; }

    /// <summary>UTC timestamp when the record was created (batch save time).</summary>
    public DateTime CreatedAt { get; set; }

    // --- Soft-delete fields (present but never set by MVP) ---

    /// <summary>Reserved for future use; never set in MVP. Attendance records are permanently immutable.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Reserved for future use; never set in MVP.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Reserved for future use; never set in MVP.</summary>
    public string? DeletedBy { get; set; }

    // --- Navigation ---

    /// <summary>The rehearsal this attendance record belongs to.</summary>
    public Rehearsal Rehearsal { get; set; } = null!;

    /// <summary>The member this attendance record concerns.</summary>
    public Member Member { get; set; } = null!;
}

namespace StageFright.Core.Entities;

/// <summary>
/// Records whether a specific member attended a specific AGM.
/// Immutable after save — no edit UI exists. The combination
/// (AnnualGeneralMeetingId, MemberId) is unique.
/// Soft-delete fields are present per Constitution §3.4 but are never independently
/// set — archiving cascades from the owning AGM only.
/// </summary>
public class AgmAttendanceRecord
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the AGM this record belongs to.</summary>
    public Guid AnnualGeneralMeetingId { get; set; }

    /// <summary>FK to the member this attendance record concerns.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Whether the member attended the AGM.</summary>
    public bool Attended { get; set; }

    /// <summary>UTC timestamp when the record was created (AGM save time).</summary>
    public DateTime CreatedAt { get; set; }

    // --- Soft-delete fields (present but never independently set) ---

    /// <summary>Reserved; never independently set. AGM attendance records are permanently immutable.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Reserved; never independently set.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Reserved; never independently set.</summary>
    public string? DeletedBy { get; set; }

    // --- Navigation ---

    /// <summary>The AGM this attendance record belongs to.</summary>
    public AnnualGeneralMeeting AnnualGeneralMeeting { get; set; } = null!;

    /// <summary>The member this attendance record concerns.</summary>
    public Member Member { get; set; } = null!;
}

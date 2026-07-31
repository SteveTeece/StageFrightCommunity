namespace StageFright.Core.Entities;

/// <summary>
/// One AGM sitting — meeting date, notes, attendance, and (via the CommitteeTerm it starts)
/// the committee positions elected at it. Replaces recording an AGM as a generic Event.
/// </summary>
public class AnnualGeneralMeeting
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Date the meeting was held. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Optional free-text notes about the meeting.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Snapshot of Settings.GeneralCommitteeSeatCountTarget at the moment this AGM was
    /// saved (FR-014). Never recomputed — reviewing a past AGM always shows the target
    /// as it was at that meeting, unaffected by later Settings changes.
    /// </summary>
    public int? GeneralCommitteeSeatCountTarget { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when this AGM has been archived (FR-017).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the AGM was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the AGM.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Attendance records for this AGM, one per active member at save time.</summary>
    public ICollection<AgmAttendanceRecord> AttendanceRecords { get; set; } = new List<AgmAttendanceRecord>();
}

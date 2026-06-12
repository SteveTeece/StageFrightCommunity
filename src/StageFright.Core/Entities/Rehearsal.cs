namespace StageFright.Core.Entities;

/// <summary>
/// A scheduled rehearsal session. Attendance recording freezes StoredAttendanceRate
/// immutably; the field remains null until attendance is first recorded.
/// </summary>
public class Rehearsal
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Date of the rehearsal (UTC).</summary>
    public DateTime Date { get; set; }

    /// <summary>Time of day the rehearsal begins.</summary>
    public TimeSpan Time { get; set; }

    /// <summary>Optional free-text notes about the rehearsal.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Attendance rate frozen at the time attendance is recorded.
    /// Formula: members present ÷ active-as-of-date count × 100.
    /// Archived members are always excluded from the denominator.
    /// Null until attendance is first saved; never recalculated.
    /// </summary>
    public decimal? StoredAttendanceRate { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the rehearsal has been archived.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the rehearsal was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the rehearsal.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>All attendance records for this rehearsal.</summary>
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

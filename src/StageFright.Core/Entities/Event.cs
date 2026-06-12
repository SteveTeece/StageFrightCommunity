namespace StageFright.Core.Entities;

/// <summary>
/// A scheduled performance or group event. Events never generate fees.
/// StoredParticipationRate is frozen once participation is recorded.
/// AGM events (EventType.IsSystemDefault with name "Annual General Meeting") drive the
/// committee-reset reminder banner (FR-031).
/// </summary>
public class Event
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Date of the event (UTC).</summary>
    public DateTime Date { get; set; }

    /// <summary>FK to the event type (e.g., Performance, AGM).</summary>
    public Guid EventTypeId { get; set; }

    /// <summary>Optional free-text notes about the event.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Participation rate frozen when participation is first recorded.
    /// Formula: participants ÷ active-as-of-date count × 100.
    /// Null until participation is saved; never recalculated.
    /// </summary>
    public decimal? StoredParticipationRate { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the event has been archived.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the event was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the event.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The event type classification.</summary>
    public EventType EventType { get; set; } = null!;

    /// <summary>All participation records for this event.</summary>
    public ICollection<ParticipationRecord> ParticipationRecords { get; set; } = new List<ParticipationRecord>();
}

namespace StageFright.Core.Entities;

/// <summary>
/// Classification of an event (e.g., Performance, Eisteddfod, Annual General Meeting).
/// System defaults (IsSystemDefault=true) are seeded at first-run setup and cannot be archived.
/// Archive is blocked while any non-deleted Event references this type.
/// </summary>
public class EventType
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Display name. Required; unique among non-deleted event types.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// True for seeded system defaults: Performance, Eisteddfod, Fund raiser,
    /// Promotional, Annual General Meeting. System defaults cannot be archived or renamed.
    /// </summary>
    public bool IsSystemDefault { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the event type has been archived.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the event type was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the event type.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Events classified under this type.</summary>
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

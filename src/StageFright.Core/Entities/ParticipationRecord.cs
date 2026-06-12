namespace StageFright.Core.Entities;

/// <summary>
/// Records whether a specific member participated in a specific event.
/// The combination (EventId, MemberId) is unique.
/// No fees are created for events.
/// </summary>
public class ParticipationRecord
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the event this record belongs to.</summary>
    public Guid EventId { get; set; }

    /// <summary>FK to the member this participation record concerns.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Whether the member participated in the event.</summary>
    public bool Participated { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the participation record has been archived.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the record was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the record.</summary>
    public string? DeletedBy { get; set; }

    // --- Navigation ---

    /// <summary>The event this participation record belongs to.</summary>
    public Event Event { get; set; } = null!;

    /// <summary>The member this participation record concerns.</summary>
    public Member Member { get; set; } = null!;
}

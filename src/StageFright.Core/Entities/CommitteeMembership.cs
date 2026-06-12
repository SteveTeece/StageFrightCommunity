namespace StageFright.Core.Entities;

/// <summary>
/// Records a member's committee position for a given calendar year.
/// The combination (MemberId, Year) is unique. Soft-deleted during annual reset
/// and when the parent member is archived.
/// </summary>
public class CommitteeMembership
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the member who holds this position.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Calendar year of the committee assignment (e.g., 2026).</summary>
    public int Year { get; set; }

    /// <summary>Committee role or title. Required when the record exists. Max 100 characters.</summary>
    public string Position { get; set; } = string.Empty;

    // --- Soft-delete fields ---

    /// <summary>True when this position record has been archived or reset.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the record was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that performed the soft-delete ("system" in MVP).</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The member this committee position belongs to.</summary>
    public Member Member { get; set; } = null!;
}

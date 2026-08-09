namespace StageFright.Core.Entities;

/// <summary>
/// The elected outcome for one member, in one named position (or general committee
/// member when OfficeHolderTypeId is null), for one committee term. Year/Position are
/// legacy-only: populated on rows created before AGM-to-AGM term tracking existed,
/// always null on rows this feature creates. A row's era is determined by whether
/// CommitteeTermId is set. Carries StartDate/EndDate so a term with a mid-term
/// replacement (special election) can hold more than one record for the same slot,
/// each dated to when that person actually served.
/// </summary>
public class CommitteePositionRecord
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the member who holds this position.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Legacy only. Calendar year of the committee assignment (e.g., 2026). Null on rows created by this feature.</summary>
    public int? Year { get; set; }

    /// <summary>Legacy only. Committee role or title, max 100 characters. Null on rows created by this feature.</summary>
    public string? Position { get; set; }

    /// <summary>FK to the committee term this record belongs to. Null on legacy rows, required on rows this feature creates.</summary>
    public Guid? CommitteeTermId { get; set; }

    /// <summary>FK to the office-holder title held. Null means general committee member.</summary>
    public Guid? OfficeHolderTypeId { get; set; }

    /// <summary>The date this member's service in this slot began (the owning AGM's date, or a special election's replacement date).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Null while this holder is current for the slot. Set only by a special election closing out a departing holder.</summary>
    public DateTime? EndDate { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when this position record has been archived.</summary>
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

    /// <summary>The committee term this record belongs to (null on legacy rows).</summary>
    public CommitteeTerm? CommitteeTerm { get; set; }

    /// <summary>The office-holder title held (null means general committee member).</summary>
    public CommitteeOfficeHolderType? OfficeHolderType { get; set; }
}

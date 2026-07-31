namespace StageFright.Core.Entities;

/// <summary>
/// The AGM-to-AGM cycle a set of committee position records belongs to. Every saved AGM
/// creates exactly one new term (StartedByAgmId), which automatically closes whatever
/// term was previously open (EndDate = the new AGM's date) — the mechanism that makes
/// "current committee" simply "the term with EndDate == null," with no separate supersede
/// operation. Historical (pre-feature) committee data has no CommitteeTerm row at all.
/// </summary>
public class CommitteeTerm
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the AGM that began this term. Exactly one starting AGM per term.</summary>
    public Guid StartedByAgmId { get; set; }

    /// <summary>Start of the term — equal to the starting AGM's date. Denormalized for query convenience.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End of the term. Null while current/open. Set to the next AGM's date the instant
    /// that next AGM is saved — this is the term-supersede mechanism.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Calendar year label, computed once at creation per the "majority of days" rule
    /// (the year following the AGM when it falls July–December, the AGM's own year
    /// when it falls January–June). Never recomputed.
    /// </summary>
    public int LabelYear { get; set; }

    // --- Audit fields (no soft-delete — a term is archived only as a side effect of
    // archiving its starting AGM) ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The AGM that started this term.</summary>
    public AnnualGeneralMeeting StartedByAgm { get; set; } = null!;

    /// <summary>Committee position records belonging to this term.</summary>
    public ICollection<CommitteePositionRecord> PositionRecords { get; set; } = new List<CommitteePositionRecord>();
}

namespace StageFright.Core.Entities;

/// <summary>
/// A defined committee position title an organisation can elect members into at an AGM
/// (e.g., "Vice President"), beyond the three built-in positions (President, Secretary,
/// Treasurer). Exactly one member may hold a given title at a time within a committee term.
/// Built-in rows (IsBuiltIn=true) cannot be renamed, reordered ahead of custom titles, or archived.
/// </summary>
public class CommitteeOfficeHolderType
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Display name. Required, max 100 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sort position. Built-ins are fixed at 0 (President), 1 (Secretary), 2 (Treasurer).
    /// Custom titles start at 3+ and may only be reordered among themselves.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>True for the three seeded built-in positions (President, Secretary, Treasurer).</summary>
    public bool IsBuiltIn { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when this title has been archived. Always false for a built-in row.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the title was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the title.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Committee position records assigned to this office-holder title across all terms.</summary>
    public ICollection<CommitteePositionRecord> PositionRecords { get; set; } = new List<CommitteePositionRecord>();
}

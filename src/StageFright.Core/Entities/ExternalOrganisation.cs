namespace StageFright.Core.Entities;

/// <summary>
/// An external body the society deals with: a venue, supplier, council, sponsor or other group.
/// Named <c>ExternalOrganisation</c> so it cannot be confused with <c>Settings.OrganizationName</c>,
/// which is the society itself. Archiving is a soft delete; contacts and linked journal entries
/// are left untouched (spec 032).
/// </summary>
public class ExternalOrganisation
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name. Required, trimmed, max 200 characters. Unique among active organisations,
    /// compared case-insensitively after trimming (FR-003).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional multi-line postal address, line breaks kept. Max 1000 characters; blank is stored as null.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// Optional address for receipts and correspondence, stored for reference only.
    /// Trimmed, max 254 characters, format-checked when present; blank is stored as null.
    /// </summary>
    public string? Email { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the organisation has been archived.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the organisation was archived. Cleared on restore.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the organisation. Cleared on restore.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent edit, archive or restore.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>People at this organisation.</summary>
    public ICollection<ExternalOrganisationContact> Contacts { get; set; } = new List<ExternalOrganisationContact>();

    /// <summary>Expense payment and income journal entries linked to this organisation.</summary>
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}

namespace StageFright.Core.Entities;

/// <summary>
/// A person at an <see cref="ExternalOrganisation"/>, such as a venue manager or a sponsor's
/// marketing officer. A contact belongs to exactly one organisation, set on create and never changed.
/// Removing a contact is a soft delete; contacts keep their own state when their organisation is
/// archived or restored.
/// </summary>
public class ExternalOrganisationContact
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the owning <see cref="ExternalOrganisation"/>. Set on create and never changed.</summary>
    public Guid ExternalOrganisationId { get; set; }

    /// <summary>Contact name. Required, trimmed, max 200 characters. Not unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person's role at the organisation. Max 100 characters; blank is stored as null.</summary>
    public string? Role { get; set; }

    /// <summary>Phone number as free text (no format check). Max 50 characters; blank is stored as null.</summary>
    public string? Phone { get; set; }

    /// <summary>Mobile number as free text (no format check). Max 50 characters; blank is stored as null.</summary>
    public string? Mobile { get; set; }

    /// <summary>Email address, format-checked when present. Max 254 characters; blank is stored as null.</summary>
    public string? Email { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the contact has been removed.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the contact was removed.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that removed the contact.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The owning organisation.</summary>
    public ExternalOrganisation ExternalOrganisation { get; set; } = null!;
}

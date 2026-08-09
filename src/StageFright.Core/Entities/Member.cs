using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// A member of the performing arts group. Supports Active/Inactive status transitions
/// and soft-delete (archival). Financial history is retained on archive.
/// </summary>
public class Member
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Given name of the member. Required, max 100 characters.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name of the member. Required, max 100 characters.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Entry-order display name: "{FirstName} {LastName}". Not mapped by EF.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>Sortable display name: "{LastName}, {FirstName}" (or just one side when the other is empty). Not mapped by EF.</summary>
    public string SortableFullName =>
        string.IsNullOrEmpty(LastName) ? FirstName :
        string.IsNullOrEmpty(FirstName) ? LastName :
        $"{LastName}, {FirstName}";

    /// <summary>Street address. Required.</summary>
    public string StreetAddress { get; set; } = string.Empty;

    /// <summary>Phone number. Optional; format validated when provided.</summary>
    public string? Phone { get; set; }

    /// <summary>Email address. Optional; format validated when provided.</summary>
    public string? Email { get; set; }

    /// <summary>Date the member joined the group. Required.</summary>
    public DateTime JoinDate { get; set; }

    /// <summary>
    /// Date of birth. Optional. When provided: must be in the past (UTC), within
    /// Settings.MaxAgeRangeYears, and resulting age must be ≥ Settings.MinimumMemberAge.
    /// </summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Current lifecycle status. Defaults to Active on creation.</summary>
    public MemberStatus Status { get; set; } = MemberStatus.Active;

    /// <summary>
    /// UTC date of the most recent Inactive→Active transition (or initial creation).
    /// Set by the system; immutable once written.
    /// </summary>
    public DateTime? ActivateDate { get; set; }

    /// <summary>
    /// UTC date of the most recent Active→Inactive transition.
    /// Set by the system; immutable once written.
    /// </summary>
    public DateTime? InactivateDate { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the member has been archived (soft-deleted).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the member was archived. Null while active.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the member ("system" in MVP). Null while active.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Committee positions held by this member across all terms (and legacy years).</summary>
    public ICollection<CommitteePositionRecord> CommitteePositionRecords { get; set; } = new List<CommitteePositionRecord>();
}

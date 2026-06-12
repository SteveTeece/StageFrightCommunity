using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// An income or expense category with an auto-assigned GL account number.
/// System categories (IsSystem=true) are seeded at first-run setup and cannot be edited
/// or archived. User categories are archivable only when no Transaction references them.
/// Archive is permanently blocked once any Transaction references the category.
/// </summary>
public class Category
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the category. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Income or Expense. Set at creation; immutable (drives the GL account range).
    /// </summary>
    public CategoryType Type { get; set; }

    /// <summary>
    /// GL account number. Auto-assigned by GLAccountAssignmentService at creation:
    /// Income → "1000", "1001", … (sequential by CreatedAt ASC);
    /// Expense → "2000", "2001", …
    /// Fixed system accounts: Cash="0100", MemberReceivable="0101", BadDebtExpense="9900".
    /// Immutable once assigned.
    /// </summary>
    public string GLAccount { get; set; } = string.Empty;

    /// <summary>User-defined sort order. Supports drag-reorder in the UI.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// True for seeded system categories (Cash, MemberReceivable, BadDebtExpense).
    /// System categories cannot be edited, archived, or reordered.
    /// </summary>
    public bool IsSystem { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the category has been archived. Permanently blocked if referenced by any Transaction.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the category was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the category.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>All GL transactions classified under this category.</summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

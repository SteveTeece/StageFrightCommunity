namespace StageFright.Core.Entities;

/// <summary>
/// Represents an expense or income category for financial tracking.
/// Supports archival and GL account mapping.
/// </summary>
public class Category
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty; // CategoryType enum as string (Income or Expense)
	public int SortOrder { get; set; }
	public bool IsArchived { get; set; }
	public string? GlAccount { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}

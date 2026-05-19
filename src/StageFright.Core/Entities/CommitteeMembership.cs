namespace StageFright.Core.Entities;

/// <summary>
/// Represents a member's committee position for a specific year.
/// Supports tracking committee membership history and annual reset.
/// </summary>
public class CommitteeMembership
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid MemberId { get; set; }
	public int Year { get; set; }
	public string Position { get; set; } = string.Empty;
	public bool IsDeleted { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
	public string? DeletedBy { get; set; }
}

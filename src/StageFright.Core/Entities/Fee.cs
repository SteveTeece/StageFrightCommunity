namespace StageFright.Core.Entities;

/// <summary>
/// Represents a fee owed by a member (Annual or Attendance).
/// Immutable after creation (NO soft-delete).
/// </summary>
public class Fee
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid MemberId { get; set; }
	public string FeeType { get; set; } = string.Empty; // FeeType enum as string
	public decimal Amount { get; set; }
	public DateTime FeeDate { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime CreatedAt { get; set; }
}

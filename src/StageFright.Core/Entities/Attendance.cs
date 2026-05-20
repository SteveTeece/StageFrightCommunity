namespace StageFright.Core.Entities;

/// <summary>
/// Represents a member's attendance at a rehearsal.
/// Immutable after creation (NO soft-delete fields).
/// </summary>
public class Attendance
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid RehearsalId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime RecordedAt { get; set; }
	public string PaidStatus { get; set; } = "Paid"; // Paid|Unpaid (set at recording time, immutable)
}

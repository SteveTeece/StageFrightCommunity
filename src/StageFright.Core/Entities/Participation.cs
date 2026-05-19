namespace StageFright.Core.Entities;

/// <summary>
/// Represents a member's participation in a public event.
/// </summary>
public class Participation
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid EventId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime RecordedAt { get; set; }
}

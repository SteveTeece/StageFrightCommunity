namespace StageFright.Core.Entities;

/// <summary>
/// Represents a public event or performance.
/// StoredParticipationRate is calculated at recording time and immutable.
/// </summary>
public class Event
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string? Notes { get; set; }
	public decimal StoredParticipationRate { get; set; } = 0m; // Percentage (0-100), immutable, calculated at recording time
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}

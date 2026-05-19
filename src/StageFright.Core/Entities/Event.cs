namespace StageFright.Core.Entities;

/// <summary>
/// Represents a public event or performance.
/// </summary>
public class Event
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string? Notes { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}

namespace StageFright.Core.Entities;

/// <summary>
/// Represents a rehearsal event scheduled for the organization.
/// </summary>
public class Rehearsal
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public TimeSpan Time { get; set; }
	public string? Notes { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}

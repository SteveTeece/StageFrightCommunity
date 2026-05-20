namespace StageFright.Core.Entities;

/// <summary>
/// Represents a rehearsal event scheduled for the organization.
/// StoredAttendanceRate is calculated at recording time and immutable.
/// </summary>
public class Rehearsal
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public TimeSpan Time { get; set; }
	public string? Notes { get; set; }
	public decimal StoredAttendanceRate { get; set; } = 0m; // Percentage (0-100), immutable, calculated at recording time
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}

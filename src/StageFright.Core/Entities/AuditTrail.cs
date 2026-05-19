namespace StageFright.Core.Entities;

/// <summary>
/// Represents an audit trail entry for data modifications.
/// Maintains a 12-month history of changes to entities.
/// </summary>
public class AuditTrail
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string EntityType { get; set; } = string.Empty;
	public Guid EntityId { get; set; }
	public string Action { get; set; } = string.Empty; // AuditAction enum as string
	public string? UserId { get; set; }
	public DateTime Timestamp { get; set; }
	public string? OldValue { get; set; }
	public string? NewValue { get; set; }
}

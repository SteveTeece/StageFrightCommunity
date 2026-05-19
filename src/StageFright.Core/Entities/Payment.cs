namespace StageFright.Core.Entities;

/// <summary>
/// Represents a payment received from a member.
/// Amount, Date, and Category are immutable after creation.
/// Only Notes can be updated after creation.
/// </summary>
public class Payment
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public decimal Amount { get; set; }
	public string PaymentMethod { get; set; } = "Cash"; // PaymentMethod enum as string
	public string PaymentType { get; set; } = string.Empty; // PaymentType enum as string
	public Guid MemberId { get; set; }
	public string Category { get; set; } = string.Empty;
	public string? Notes { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

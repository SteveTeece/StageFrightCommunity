namespace StageFright.Core.Entities;

/// <summary>
/// Represents a General Ledger transaction (double-entry accounting).
/// GL transactions are paired (debit/credit) and immutable after creation.
/// </summary>
public class Transaction
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime Date { get; set; }
	public string Category { get; set; } = string.Empty;
	public decimal? DebitAmount { get; set; }
	public decimal? CreditAmount { get; set; }
	public Guid? MemberId { get; set; }
	public Guid? PaymentId { get; set; }
	public string? Description { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
}

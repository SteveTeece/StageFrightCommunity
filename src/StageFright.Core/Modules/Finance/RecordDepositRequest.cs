namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input for banking received items and/or Petty Cash into a bank account (spec 033).
/// </summary>
public class RecordDepositRequest
{
    /// <summary>Deposit date. Not before any included item's date; inside an open period.</summary>
    public DateTime Date { get; set; }

    /// <summary>The receiving bank account. Must be a bank account other than Petty Cash.</summary>
    public Guid BankAccountId { get; set; }

    /// <summary>Received items to include. Each must still be awaiting deposit.</summary>
    public IReadOnlyList<Guid> ReceivedItemIds { get; set; } = [];

    /// <summary>Petty Cash to include. Zero or more, up to the cash available.</summary>
    public decimal CashAmount { get; set; }

    /// <summary>Optional note for the journal entry.</summary>
    public string? Description { get; set; }
}

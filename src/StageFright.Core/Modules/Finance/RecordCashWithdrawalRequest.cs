namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input for withdrawing cash from a bank account into Petty Cash, e.g. a concert float (spec 033).
/// </summary>
public class RecordCashWithdrawalRequest
{
    /// <summary>Withdrawal date. Inside an open period.</summary>
    public DateTime Date { get; set; }

    /// <summary>The bank account withdrawn from. Must be a bank account other than Petty Cash.</summary>
    public Guid BankAccountId { get; set; }

    /// <summary>Amount withdrawn. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Optional purpose (max 200), stored as the journal entry's description.</summary>
    public string? Purpose { get; set; }
}

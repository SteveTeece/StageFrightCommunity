namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for depositing collected Cash on Hand into a bank account.
/// </summary>
public class RecordBankDepositRequest
{
    /// <summary>UTC date of the deposit. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount deposited. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Id of the destination bank account. Must differ from Cash on Hand.</summary>
    public Guid ToAccountId { get; set; }

    /// <summary>Optional description for the GL transactions.</summary>
    public string? Description { get; set; }
}

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for transferring funds between two bank/cash accounts.
/// </summary>
public class RecordTransferRequest
{
    /// <summary>UTC date of the transfer. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount transferred. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Id of the bank/cash account the funds leave. Must differ from the destination.</summary>
    public Guid FromAccountId { get; set; }

    /// <summary>Id of the bank/cash account the funds arrive in.</summary>
    public Guid ToAccountId { get; set; }

    /// <summary>Optional description for the GL transactions.</summary>
    public string? Description { get; set; }
}

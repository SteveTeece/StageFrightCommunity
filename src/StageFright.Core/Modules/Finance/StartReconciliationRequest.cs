namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for starting a new draft bank reconciliation.
/// </summary>
public class StartReconciliationRequest
{
    /// <summary>The bank/cash account to reconcile. Must have IsBankAccount=true.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Closing date of the bank statement. Must be after the last finalised statement date.</summary>
    public DateTime StatementDate { get; set; }

    /// <summary>Closing balance printed on the bank statement.</summary>
    public decimal StatementClosingBalance { get; set; }

    /// <summary>Optional free-form notes.</summary>
    public string? Notes { get; set; }
}

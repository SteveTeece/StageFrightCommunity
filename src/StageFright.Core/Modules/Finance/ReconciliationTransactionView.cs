using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// One candidate GL transaction in a reconciliation workspace, with its cleared state.
/// </summary>
public class ReconciliationTransactionView
{
    /// <summary>The GL transaction on the bank account.</summary>
    public required Transaction Transaction { get; init; }

    /// <summary>True when the transaction is ticked off in the current reconciliation.</summary>
    public bool IsCleared { get; init; }
}

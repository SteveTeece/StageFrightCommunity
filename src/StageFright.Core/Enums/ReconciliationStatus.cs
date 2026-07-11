namespace StageFright.Core.Enums;

/// <summary>
/// Lifecycle state of a bank reconciliation.
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>In progress — lines may be added/removed and the draft may be deleted.</summary>
    Draft,

    /// <summary>Completed with a difference of $0.00 — immutable and undeletable.</summary>
    Finalised
}

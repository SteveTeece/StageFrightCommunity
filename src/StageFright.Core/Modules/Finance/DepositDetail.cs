namespace StageFright.Core.Modules.Finance;

/// <summary>
/// A single deposit as shown on its detail page (spec 033). Never persisted.
/// </summary>
public class DepositDetail
{
    /// <summary>The <see cref="Entities.BankDeposit"/> id, or the journal entry id for a legacy deposit.</summary>
    public Guid Id { get; init; }

    /// <summary>The deposit date.</summary>
    public DateTime Date { get; init; }

    /// <summary>The bank account deposited into.</summary>
    public string BankAccountName { get; init; } = string.Empty;

    /// <summary>Petty Cash banked in this deposit.</summary>
    public decimal CashAmount { get; init; }

    /// <summary>The received items banked in this deposit. Empty for a legacy deposit.</summary>
    public IReadOnlyList<AwaitingDepositItem> Items { get; init; } = [];

    /// <summary>The amount banked: the cash amount plus the items.</summary>
    public decimal Total { get; init; }

    /// <summary>True once the deposit has been reversed.</summary>
    public bool IsReversed { get; init; }

    /// <summary>UTC timestamp of the reversal, when reversed.</summary>
    public DateTime? ReversedAt { get; init; }

    /// <summary>True for a deposit posted before spec 033, which has no deposit record.</summary>
    public bool IsLegacy { get; init; }

    /// <summary>True when the reverse action is allowed now.</summary>
    public bool CanReverse { get; init; }

    /// <summary>Translated reason the deposit cannot be reversed (reconciled, closed period, already reversed, legacy). Null when <see cref="CanReverse"/>.</summary>
    public string? CannotReverseReason { get; init; }
}

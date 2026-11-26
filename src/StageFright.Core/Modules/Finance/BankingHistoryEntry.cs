using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// One row of the Banking history: a deposit or a cash withdrawal (spec 033). Never persisted.
/// </summary>
public class BankingHistoryEntry
{
    /// <summary>The <see cref="Entities.BankDeposit"/> id, or the journal entry id for a withdrawal or legacy deposit.</summary>
    public Guid Id { get; init; }

    /// <summary>Deposit or withdrawal.</summary>
    public BankingHistoryEntryType Type { get; init; }

    /// <summary>The posting date.</summary>
    public DateTime Date { get; init; }

    /// <summary>The bank account deposited into or withdrawn from.</summary>
    public string BankAccountName { get; init; } = string.Empty;

    /// <summary>The amount banked or withdrawn.</summary>
    public decimal Total { get; init; }

    /// <summary>True once a deposit has been reversed. Always false for a withdrawal.</summary>
    public bool IsReversed { get; init; }

    /// <summary>True for a deposit posted before spec 033, which has a journal entry but no deposit record and cannot be reversed.</summary>
    public bool IsLegacy { get; init; }

    /// <summary>The journal description: a withdrawal's purpose, or a deposit's note.</summary>
    public string? Description { get; init; }
}

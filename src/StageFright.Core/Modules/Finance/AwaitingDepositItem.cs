using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// A received item (cheque or other) as shown in the Banking lists (spec 033). Never persisted.
/// </summary>
public class AwaitingDepositItem
{
    /// <summary>The <see cref="Entities.ReceivedItem"/> id.</summary>
    public Guid Id { get; init; }

    /// <summary>The date the receipt was posted.</summary>
    public DateTime ReceivedDate { get; init; }

    /// <summary>Where the item came from. The page shows a translated label for <see cref="ReceivedItemSource.OpeningBalance"/>.</summary>
    public ReceivedItemSource Source { get; init; }

    /// <summary>The member's name, or the organisation's name, or the income description. Null for the opening balance.</summary>
    public string? PayerName { get; init; }

    /// <summary>The cheque number or note, if one was entered.</summary>
    public string? Reference { get; init; }

    /// <summary>The amount waiting in Undeposited Funds.</summary>
    public decimal Amount { get; init; }
}

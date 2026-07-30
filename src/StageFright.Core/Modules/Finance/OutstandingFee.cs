using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// A member's outstanding fee with its true remaining amount owed (original amount
/// minus prior GL settlements) — a read-only projection, never persisted.
/// </summary>
public class OutstandingFee
{
    /// <summary>The underlying <see cref="Entities.Fee"/>'s primary key.</summary>
    public Guid FeeId { get; init; }

    /// <summary>Classification of the fee, copied from <see cref="Entities.Fee.FeeType"/>.</summary>
    public FeeType FeeType { get; init; }

    /// <summary>Date the fee applies to, copied from <see cref="Entities.Fee.FeeDate"/>.</summary>
    public DateTime FeeDate { get; init; }

    /// <summary>Date by which payment is expected, copied from <see cref="Entities.Fee.DueDate"/>.</summary>
    public DateTime DueDate { get; init; }

    /// <summary>Fee.Amount minus the sum of MemberReceivable GL credits already applied to this fee. Always &gt; 0.</summary>
    public decimal RemainingAmount { get; init; }
}

namespace StageFright.Core.Entities;

/// <summary>
/// Links one <see cref="BankDeposit"/> to one <see cref="ReceivedItem"/> that was banked in it.
/// A financial record: immutable and never deleted, so it carries NO soft-delete fields
/// (Constitution §3.4). The unique index is on (<see cref="BankDepositId"/>,
/// <see cref="ReceivedItemId"/>) only; <see cref="ReceivedItemId"/> alone is not unique
/// because an item can be deposited again after its earlier deposit is reversed.
/// </summary>
public class BankDepositLine
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the deposit this line belongs to.</summary>
    public Guid BankDepositId { get; set; }

    /// <summary>FK to the received item that was banked.</summary>
    public Guid ReceivedItemId { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>The deposit this line belongs to.</summary>
    public BankDeposit? BankDeposit { get; set; }

    /// <summary>The received item banked by this line.</summary>
    public ReceivedItem? ReceivedItem { get; set; }
}

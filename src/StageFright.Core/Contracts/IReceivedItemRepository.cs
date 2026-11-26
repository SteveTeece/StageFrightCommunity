using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository for received items: cheques and other receipts held in Undeposited Funds (spec 033).
/// An item is awaiting deposit while no line of an unreversed <see cref="BankDeposit"/> includes it.
/// Items are never updated or deleted.
/// </summary>
public interface IReceivedItemRepository
{
    /// <summary>Adds a received item.</summary>
    Task<ReceivedItem> AddAsync(ReceivedItem item, CancellationToken ct = default);

    /// <summary>
    /// Returns every item awaiting deposit, oldest first, with the payer data loaded:
    /// <c>Payment.Member</c> and <c>JournalEntry.ExternalOrganisation</c> (archived included).
    /// </summary>
    Task<IReadOnlyList<ReceivedItem>> GetAwaitingAsync(CancellationToken ct = default);

    /// <summary>Returns the items among <paramref name="ids"/> that are still awaiting deposit. Unknown or already-deposited ids are left out.</summary>
    Task<IReadOnlyList<ReceivedItem>> GetAwaitingByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

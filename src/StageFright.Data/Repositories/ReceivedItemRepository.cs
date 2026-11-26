using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class ReceivedItemRepository : IReceivedItemRepository
{
    private readonly StageFrightDbContext _db;

    public ReceivedItemRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<ReceivedItem> AddAsync(ReceivedItem item, CancellationToken ct = default)
    {
        try
        {
            var added = await _db.ReceivedItems.AddAsync(item, ct);
            await _db.SaveChangesAsync(ct);
            return added.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(ReceivedItem), nameof(AddAsync), item.Id, ex);
        }
    }

    public async Task<IReadOnlyList<ReceivedItem>> GetAwaitingAsync(CancellationToken ct = default)
    {
        try
        {
            return await AwaitingWithPayer().ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(ReceivedItem), nameof(GetAwaitingAsync), null, ex);
        }
    }

    public async Task<IReadOnlyList<ReceivedItem>> GetAwaitingByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        try
        {
            return await AwaitingWithPayer().Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(ReceivedItem), nameof(GetAwaitingByIdsAsync), null, ex);
        }
    }

    /// <summary>
    /// Untracked items with no line on an unreversed deposit, oldest first. Query filters are ignored so a
    /// soft-deleted member or an archived organisation still names the payer instead of hiding the item.
    /// </summary>
    private IQueryable<ReceivedItem> AwaitingWithPayer() =>
        _db.ReceivedItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(i => i.Payment!).ThenInclude(p => p.Member)
            .Include(i => i.JournalEntry!).ThenInclude(j => j.ExternalOrganisation)
            .Where(i => !_db.BankDepositLines.Any(l =>
                l.ReceivedItemId == i.Id && l.BankDeposit!.ReversalJournalEntryId == null))
            .OrderBy(i => i.Date)
            .ThenBy(i => i.CreatedAt);
}

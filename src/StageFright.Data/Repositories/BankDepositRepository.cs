using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class BankDepositRepository : IBankDepositRepository
{
    private readonly StageFrightDbContext _db;

    public BankDepositRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<BankDeposit> AddAsync(BankDeposit deposit, CancellationToken ct = default)
    {
        try
        {
            var added = await _db.BankDeposits.AddAsync(deposit, ct);
            await _db.SaveChangesAsync(ct);
            return added.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(BankDeposit), nameof(AddAsync), deposit.Id, ex);
        }
    }

    public async Task<BankDeposit?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            // Query filters are ignored so a soft-deleted bank account, member or archived organisation
            // still names the deposit's account and payers.
            return await _db.BankDeposits
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(d => d.BankAccount)
                .Include(d => d.JournalEntry!).ThenInclude(j => j.Transactions)
                .Include(d => d.Lines).ThenInclude(l => l.ReceivedItem!).ThenInclude(i => i.Payment!).ThenInclude(p => p.Member)
                .Include(d => d.Lines).ThenInclude(l => l.ReceivedItem!).ThenInclude(i => i.JournalEntry!).ThenInclude(j => j.ExternalOrganisation)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.Id == id, ct);
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(BankDeposit), nameof(GetByIdAsync), id, ex);
        }
    }

    public async Task<IReadOnlyList<BankDeposit>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.BankDeposits
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(d => d.BankAccount)
                .OrderByDescending(d => d.Date)
                .ThenByDescending(d => d.CreatedAt)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException and not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(BankDeposit), nameof(GetAllAsync), null, ex);
        }
    }

    public async Task MarkReversedAsync(Guid id, Guid reversalJournalEntryId, DateTime reversedAt, CancellationToken ct = default)
    {
        BankDeposit? deposit;
        try
        {
            deposit = await _db.BankDeposits.FirstOrDefaultAsync(d => d.Id == id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(BankDeposit), nameof(MarkReversedAsync), id, ex);
        }

        if (deposit is null)
            throw new EntityNotFoundException(nameof(BankDeposit), id, nameof(MarkReversedAsync));

        // Write-once: the reversal link is the only change a deposit ever receives.
        if (deposit.ReversalJournalEntryId is not null)
            throw new DataIntegrityException(
                "The bank deposit has already been reversed.", nameof(BankDeposit), nameof(MarkReversedAsync), id);

        try
        {
            deposit.ReversalJournalEntryId = reversalJournalEntryId;
            deposit.ReversedAt = reversedAt;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DataAccessException(ex.Message, nameof(BankDeposit), nameof(MarkReversedAsync), id, ex);
        }
    }
}

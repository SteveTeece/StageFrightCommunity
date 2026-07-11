using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class BankReconciliationRepository : IBankReconciliationRepository
{
    private readonly StageFrightDbContext _db;

    public BankReconciliationRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<BankReconciliation> CreateDraftAsync(
        Guid accountId, DateTime statementDate, decimal statementClosingBalance,
        string? notes, CancellationToken ct = default)
    {
        try
        {
            var lastFinalised = await GetLastFinalisedForAccountAsync(accountId, ct);
            var now = DateTime.UtcNow;

            var reconciliation = new BankReconciliation
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                StatementDate = statementDate,
                StatementClosingBalance = statementClosingBalance,
                OpeningBalance = lastFinalised?.StatementClosingBalance ?? 0m,
                Status = ReconciliationStatus.Draft,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            var added = await _db.BankReconciliations.AddAsync(reconciliation, ct);
            await _db.SaveChangesAsync(ct);
            return added.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException and not ReconciliationException)
        {
            throw new DataAccessException(ex.Message, nameof(BankReconciliation), nameof(CreateDraftAsync), null, ex);
        }
    }

    public async Task<BankReconciliation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.BankReconciliations
            .Include(r => r.Account)
            .Include(r => r.Lines)
            .ThenInclude(l => l.Transaction)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<BankReconciliation>> GetByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.BankReconciliations
            .Where(r => r.AccountId == accountId)
            .OrderByDescending(r => r.StatementDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<BankReconciliation?> GetDraftForAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.BankReconciliations
            .FirstOrDefaultAsync(r => r.AccountId == accountId && r.Status == ReconciliationStatus.Draft, ct);
    }

    public async Task<BankReconciliation?> GetLastFinalisedForAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.BankReconciliations
            .Where(r => r.AccountId == accountId && r.Status == ReconciliationStatus.Finalised)
            .OrderByDescending(r => r.StatementDate)
            .ThenByDescending(r => r.FinalisedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddLineAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, nameof(AddLineAsync), ct);

        try
        {
            await _db.ReconciliationLines.AddAsync(new ReconciliationLine
            {
                Id = Guid.NewGuid(),
                ReconciliationId = reconciliation.Id,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            }, ct);
            reconciliation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ReconciliationLine), nameof(AddLineAsync), reconciliationId, ex);
        }
    }

    public async Task RemoveLineAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, nameof(RemoveLineAsync), ct);

        try
        {
            var line = await _db.ReconciliationLines
                .FirstOrDefaultAsync(l => l.ReconciliationId == reconciliationId && l.TransactionId == transactionId, ct);
            if (line is null)
                return;

            _db.ReconciliationLines.Remove(line);
            reconciliation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ReconciliationLine), nameof(RemoveLineAsync), reconciliationId, ex);
        }
    }

    public async Task<bool> IsTransactionClearedElsewhereAsync(Guid transactionId, Guid excludeReconciliationId, CancellationToken ct = default)
    {
        // The global query filter on ReconciliationLine already excludes lines whose
        // parent reconciliation has been soft-deleted.
        return await _db.ReconciliationLines
            .AnyAsync(l => l.TransactionId == transactionId && l.ReconciliationId != excludeReconciliationId, ct);
    }

    public async Task FinaliseAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, nameof(FinaliseAsync), ct);

        try
        {
            var now = DateTime.UtcNow;
            reconciliation.Status = ReconciliationStatus.Finalised;
            reconciliation.FinalisedAt = now;
            reconciliation.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(BankReconciliation), nameof(FinaliseAsync), reconciliationId, ex);
        }
    }

    public async Task SoftDeleteDraftAsync(Guid reconciliationId, string deletedBy, CancellationToken ct = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, nameof(SoftDeleteDraftAsync), ct);

        try
        {
            var now = DateTime.UtcNow;
            reconciliation.IsDeleted = true;
            reconciliation.DeletedAt = now;
            reconciliation.DeletedBy = deletedBy;
            reconciliation.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(BankReconciliation), nameof(SoftDeleteDraftAsync), reconciliationId, ex);
        }
    }

    private async Task<BankReconciliation> RequireDraftAsync(Guid reconciliationId, string operation, CancellationToken ct)
    {
        var reconciliation = await _db.BankReconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId, ct)
            ?? throw new EntityNotFoundException(nameof(BankReconciliation), reconciliationId, operation);

        if (reconciliation.Status == ReconciliationStatus.Finalised)
            throw new ReconciliationException(
                "Finalised reconciliations are immutable and cannot be modified or deleted.",
                nameof(BankReconciliation), operation, reconciliationId);

        return reconciliation;
    }
}

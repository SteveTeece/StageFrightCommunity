using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;

namespace StageFright.Data.Repositories;

public class GLRepository : IGLRepository
{
    private readonly StageFrightDbContext _db;

    public GLRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task AddPairAsync(Transaction debit, Transaction credit, CancellationToken ct = default)
    {
        if (debit.DebitAmount != credit.CreditAmount)
            throw new GLBalanceException(
                "GL transaction pair imbalanced; operation cancelled.",
                nameof(Transaction), nameof(AddPairAsync));

        await AddBalancedSetAsync(new[] { debit, credit }, ct);
    }

    public async Task AddBalancedSetAsync(IReadOnlyList<Transaction> lines, CancellationToken ct = default)
    {
        if (lines.Count < 2)
            throw new GLBalanceException(
                "A GL posting requires at least two lines.",
                nameof(Transaction), nameof(AddBalancedSetAsync));

        foreach (var line in lines)
        {
            if (line.DebitAmount < 0m || line.CreditAmount < 0m)
                throw new GLBalanceException(
                    "GL line amounts cannot be negative.",
                    nameof(Transaction), nameof(AddBalancedSetAsync));

            if ((line.DebitAmount != 0m) == (line.CreditAmount != 0m))
                throw new GLBalanceException(
                    "Each GL line must have exactly one non-zero side (debit or credit).",
                    nameof(Transaction), nameof(AddBalancedSetAsync));
        }

        if (lines.Sum(l => l.DebitAmount) != lines.Sum(l => l.CreditAmount))
            throw new GLBalanceException(
                "GL set imbalanced (Σdebits ≠ Σcredits); operation cancelled.",
                nameof(Transaction), nameof(AddBalancedSetAsync));

        await _db.Transactions.AddRangeAsync(lines, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetMemberBalanceAsync(Guid memberId, CancellationToken ct = default)
    {
        // Outstanding = net balance of the Member Receivable account for this member.
        // Debits create the receivable; credits clear it on payment/forgiveness.
        // Keyed on AccountId — the denormalized GLAccount string is a posting-time
        // snapshot and legacy rows still carry the old "0101" number.
        var debits = await _db.Transactions
            .Where(t => t.MemberId == memberId && t.AccountId == SystemAccounts.MemberReceivableId)
            .SumAsync(t => t.DebitAmount, ct);

        var credits = await _db.Transactions
            .Where(t => t.MemberId == memberId && t.AccountId == SystemAccounts.MemberReceivableId)
            .SumAsync(t => t.CreditAmount, ct);

        return debits - credits;
    }

    public async Task<decimal> GetTotalOutstandingAsync(CancellationToken ct = default)
    {
        // Outstanding across all members = net balance of the Member Receivable account.
        // In a balanced double-entry GL, summing ALL accounts yields zero; we project only the
        // receivable account to get the meaningful "members owe" figure for the Finance tile.
        var debits = await _db.Transactions
            .Where(t => t.AccountId == SystemAccounts.MemberReceivableId)
            .SumAsync(t => t.DebitAmount, ct);

        var credits = await _db.Transactions
            .Where(t => t.AccountId == SystemAccounts.MemberReceivableId)
            .SumAsync(t => t.CreditAmount, ct);

        return debits - credits;
    }

    public async Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime asAt, CancellationToken ct = default)
    {
        var debits = await _db.Transactions
            .Where(t => t.AccountId == accountId && t.Date <= asAt)
            .SumAsync(t => t.DebitAmount, ct);

        var credits = await _db.Transactions
            .Where(t => t.AccountId == accountId && t.Date <= asAt)
            .SumAsync(t => t.CreditAmount, ct);

        return debits - credits;
    }

    public async Task<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>> GetAccountMovementsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var movements = await _db.Transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => t.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debits = g.Sum(t => t.DebitAmount),
                Credits = g.Sum(t => t.CreditAmount)
            })
            .ToListAsync(ct);

        return movements.ToDictionary(m => m.AccountId, m => (m.Debits, m.Credits));
    }

    public async Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.Transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByMemberAsync(Guid memberId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.Transactions
            .Where(t => t.MemberId == memberId && t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(decimal TotalDebits, decimal TotalCredits)> GetBalanceTotalsAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var totalDebits = await _db.Transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .SumAsync(t => t.DebitAmount, ct);

        var totalCredits = await _db.Transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .SumAsync(t => t.CreditAmount, ct);

        return (totalDebits, totalCredits);
    }

    public async Task<IReadOnlyList<Transaction>> GetByFeeAsync(Guid feeId, CancellationToken ct = default)
    {
        return await _db.Transactions
            .Where(t => t.FeeId == feeId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetUnreconciledByAccountAsync(
        Guid accountId, DateTime? upTo = null, CancellationToken ct = default)
    {
        // The global query filter on ReconciliationLine excludes lines belonging to
        // soft-deleted reconciliations, so those transactions reappear as unreconciled.
        var query = _db.Transactions
            .Where(t => t.AccountId == accountId)
            .Where(t => !_db.ReconciliationLines.Any(l => l.TransactionId == t.Id));

        if (upTo is not null)
            query = query.Where(t => t.Date <= upTo);

        return await query
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(decimal Current, decimal Days30, decimal Days60, decimal Days90Plus)> GetAgingBucketsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var fees = await _db.Fees
            .Where(f => !f.PaidAtCreation)
            .Select(f => new { f.Amount, f.DueDate })
            .ToListAsync(ct);

        decimal current = 0, days30 = 0, days60 = 0, days90Plus = 0;
        foreach (var fee in fees)
        {
            var daysOverdue = (today - fee.DueDate.Date).Days;
            if (daysOverdue <= 0) current += fee.Amount;
            else if (daysOverdue <= 30) days30 += fee.Amount;
            else if (daysOverdue <= 60) days60 += fee.Amount;
            else days90Plus += fee.Amount;
        }
        return (current, days30, days60, days90Plus);
    }
}

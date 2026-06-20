using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

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

        await _db.Transactions.AddRangeAsync(new[] { debit, credit }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetMemberBalanceAsync(Guid memberId, CancellationToken ct = default)
    {
        // Outstanding = net balance of the MemberReceivable account (GL#0101) for this member.
        // Debits to 0101 create the receivable; credits to 0101 clear it on payment/forgiveness.
        var debits = await _db.Transactions
            .Where(t => t.MemberId == memberId && t.GLAccount == "0101")
            .SumAsync(t => t.DebitAmount, ct);

        var credits = await _db.Transactions
            .Where(t => t.MemberId == memberId && t.GLAccount == "0101")
            .SumAsync(t => t.CreditAmount, ct);

        return debits - credits;
    }

    public async Task<decimal> GetTotalOutstandingAsync(CancellationToken ct = default)
    {
        // Outstanding across all members = net balance of the MemberReceivable account (GL#0101).
        // In a balanced double-entry GL, summing ALL accounts yields zero; we project only the
        // receivable account to get the meaningful "members owe" figure for the Finance tile.
        var debits = await _db.Transactions
            .Where(t => t.GLAccount == "0101")
            .SumAsync(t => t.DebitAmount, ct);

        var credits = await _db.Transactions
            .Where(t => t.GLAccount == "0101")
            .SumAsync(t => t.CreditAmount, ct);

        return debits - credits;
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
}

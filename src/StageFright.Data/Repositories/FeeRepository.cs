using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class FeeRepository : IFeeRepository
{
    private readonly StageFrightDbContext _db;

    public FeeRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<Fee?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Fees.FindAsync(new object[] { id }, ct);
    }

    public async Task<Fee> AddAsync(Fee fee, CancellationToken ct = default)
    {
        try
        {
            var entry = await _db.Fees.AddAsync(fee, ct);
            await _db.SaveChangesAsync(ct);
            return entry.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(Fee), nameof(AddAsync), null, ex);
        }
    }

    public async Task<IReadOnlyList<Fee>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        return await _db.Fees
            .Where(f => f.MemberId == memberId)
            .OrderBy(f => f.FeeDate)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Fee>> GetUnpaidOrderedFifoAsync(Guid memberId, CancellationToken ct = default)
    {
        // FIFO ordering; payment status is determined by GL balance in higher layers
        return await _db.Fees
            .Where(f => f.MemberId == memberId)
            .OrderBy(f => f.FeeDate)
            .ThenBy(f => f.CreatedAt)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> AnnualFeeExistsAsync(Guid memberId, int year, CancellationToken ct = default)
    {
        return await _db.Fees
            .AnyAsync(f => f.MemberId == memberId
                && f.FeeType == FeeType.Annual
                && f.FeeDate.Year == year, ct);
    }

    public async Task<bool> AttendanceFeeExistsAsync(Guid memberId, Guid rehearsalId, CancellationToken ct = default)
    {
        return await _db.Fees
            .AnyAsync(f => f.MemberId == memberId && f.RehearsalId == rehearsalId, ct);
    }

    public async Task<IReadOnlyList<Fee>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        return await _db.Fees
            .Where(f => f.RehearsalId == rehearsalId)
            .ToListAsync(ct);
    }
}

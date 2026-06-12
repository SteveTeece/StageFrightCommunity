using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class CommitteeMembershipRepository : SoftDeletableBaseRepository<CommitteeMembership>, ICommitteeMembershipRepository
{
    public CommitteeMembershipRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CommitteeMembership>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        return await _db.CommitteeMemberships
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.Year)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeMembership>> GetByYearAsync(int year, CancellationToken ct = default)
    {
        return await _db.CommitteeMemberships
            .Where(c => c.Year == year)
            .ToListAsync(ct);
    }

    public async Task SoftDeleteCurrentYearAsync(int year, string deletedBy, CancellationToken ct = default)
    {
        var records = await _db.CommitteeMemberships
            .Where(c => c.Year == year)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var record in records)
        {
            record.IsDeleted = true;
            record.DeletedAt = now;
            record.DeletedBy = deletedBy;
            record.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}

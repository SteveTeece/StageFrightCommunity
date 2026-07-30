using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Data.Repositories;

public class MemberRepository : SoftDeletableBaseRepository<Member>, IMemberRepository
{
    public MemberRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default)
    {
        return await _db.Members
            .Where(m => m.Status == status)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Member>> GetActiveAsOfAsync(DateTime date, CancellationToken ct = default)
    {
        return await _db.Members
            .Where(m =>
                (m.Status == MemberStatus.Active && m.ActivateDate <= date)
                || (m.Status == MemberStatus.Inactive && m.ActivateDate <= date && m.InactivateDate > date))
            .ToListAsync(ct);
    }
}

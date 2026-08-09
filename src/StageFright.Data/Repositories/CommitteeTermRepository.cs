using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class CommitteeTermRepository : BaseRepository<CommitteeTerm>, ICommitteeTermRepository
{
    public CommitteeTermRepository(StageFrightDbContext db) : base(db) { }

    public async Task<CommitteeTerm?> GetOpenAsync(CancellationToken ct = default)
    {
        return await _db.CommitteeTerms
            .FirstOrDefaultAsync(t => t.EndDate == null, ct);
    }
}

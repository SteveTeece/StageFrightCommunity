using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class CommitteeOfficeHolderTypeRepository : SoftDeletableBaseRepository<CommitteeOfficeHolderType>, ICommitteeOfficeHolderTypeRepository
{
    public CommitteeOfficeHolderTypeRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CommitteeOfficeHolderType>> GetActiveOrderedAsync(CancellationToken ct = default)
    {
        return await _db.CommitteeOfficeHolderTypes
            .OrderByDescending(t => t.IsBuiltIn)
            .ThenBy(t => t.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<int?> GetMaxCustomDisplayOrderAsync(CancellationToken ct = default)
    {
        var customOrders = await _db.CommitteeOfficeHolderTypes
            .IgnoreQueryFilters()
            .Where(t => !t.IsBuiltIn)
            .Select(t => (int?)t.DisplayOrder)
            .ToListAsync(ct);

        return customOrders.Count > 0 ? customOrders.Max() : null;
    }
}

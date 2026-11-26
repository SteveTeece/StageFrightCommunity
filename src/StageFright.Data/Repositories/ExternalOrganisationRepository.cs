using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

/// <summary>
/// Organisation reads always load <see cref="ExternalOrganisation.Contacts"/> limited to
/// non-removed contacts, so the list grid's contact count (FR-001) never counts removed ones.
/// Reads that bypass the organisation's soft-delete filter must filter the include by hand,
/// because <c>IgnoreQueryFilters</c> also drops the contacts' own filter.
/// </summary>
public class ExternalOrganisationRepository : SoftDeletableBaseRepository<ExternalOrganisation>, IExternalOrganisationRepository
{
    public ExternalOrganisationRepository(StageFrightDbContext db) : base(db) { }

    public override async Task<IReadOnlyList<ExternalOrganisation>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.ExternalOrganisations
                .Include(o => o.Contacts)
                .OrderBy(o => o.Name)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ExternalOrganisation), nameof(GetAllAsync), null, ex);
        }
    }

    public override async Task<IReadOnlyList<ExternalOrganisation>> GetArchivedAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.ExternalOrganisations
                .IgnoreQueryFilters()
                .Where(o => o.IsDeleted)
                .Include(o => o.Contacts.Where(c => !c.IsDeleted))
                .OrderBy(o => o.Name)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ExternalOrganisation), nameof(GetArchivedAsync), null, ex);
        }
    }

    public async Task<ExternalOrganisation?> GetByIdIncludingArchivedAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _db.ExternalOrganisations
                .IgnoreQueryFilters()
                .Include(o => o.Contacts.Where(c => !c.IsDeleted))
                .FirstOrDefaultAsync(o => o.Id == id, ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ExternalOrganisation), nameof(GetByIdIncludingArchivedAsync), id, ex);
        }
    }

    public async Task<bool> IsActiveNameInUseAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        try
        {
            // The comparison runs in C# because SQLite NOCASE folds ASCII only (research §4).
            var activeNames = await _db.ExternalOrganisations
                .Where(o => excludeId == null || o.Id != excludeId)
                .Select(o => o.Name)
                .ToListAsync(ct);

            var trimmed = name.Trim();
            return activeNames.Any(n => string.Equals(n.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ExternalOrganisation), nameof(IsActiveNameInUseAsync), excludeId, ex);
        }
    }
}

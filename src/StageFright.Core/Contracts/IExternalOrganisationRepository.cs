using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for ExternalOrganisation entities.
/// GetAllAsync and GetArchivedAsync load Contacts filtered to non-removed contacts,
/// including for archived organisations.
/// </summary>
public interface IExternalOrganisationRepository : ISoftDeletableRepository<ExternalOrganisation>
{
    /// <summary>
    /// Returns the organisation with the given id, ignoring the soft-delete filter on the
    /// organisation. Contacts holds only non-removed contacts. Null when the id is unknown.
    /// </summary>
    Task<ExternalOrganisation?> GetByIdIncludingArchivedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns true if an active (non-archived) organisation other than <paramref name="excludeId"/>
    /// already uses <paramref name="name"/>, compared with OrdinalIgnoreCase after trimming.
    /// Archived organisations never count, so their names may be reused.
    /// </summary>
    Task<bool> IsActiveNameInUseAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
}

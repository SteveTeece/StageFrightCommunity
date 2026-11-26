using StageFright.Core.Entities;
using StageFright.Core.Modules.Organisations;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for the external organisation register (spec 032, US1): list, look up,
/// create, update, archive and restore. Every write is audited (FR-014). The financial history
/// lives on <c>IOrganisationHistoryService</c>, not here.
/// </summary>
public interface IExternalOrganisationService
{
    /// <summary>Active organisations ordered by Name, each with its non-removed Contacts loaded.</summary>
    Task<IReadOnlyList<ExternalOrganisation>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Archived organisations ordered by Name, each with its non-removed Contacts loaded.</summary>
    Task<IReadOnlyList<ExternalOrganisation>> GetArchivedAsync(CancellationToken ct = default);

    /// <summary>Active or archived organisation with its non-removed Contacts, or null when the id is unknown.</summary>
    Task<ExternalOrganisation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates an organisation. Throws ValidationException on a rule failure or a duplicate
    /// active name. Audits Create.
    /// </summary>
    Task<ExternalOrganisation> CreateAsync(SaveExternalOrganisationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an organisation. Throws EntityNotFoundException for an unknown id, and
    /// ValidationException on a rule failure, a duplicate active name (excluding itself), or an
    /// archived organisation. Audits Update.
    /// </summary>
    Task UpdateAsync(Guid id, SaveExternalOrganisationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Soft delete. Contacts and linked journal entries are untouched. Throws
    /// EntityNotFoundException for an unknown id and ValidationException when already archived.
    /// Audits Delete.
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Restores an archived organisation. Throws EntityNotFoundException for an unknown id and
    /// ValidationException (Validation_Organisation_NameDuplicate) when an active organisation now
    /// uses the name. Audits Restore.
    /// </summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}

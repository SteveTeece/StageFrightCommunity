using StageFright.Core.Entities;
using StageFright.Core.Modules.Organisations;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for the contact people on an external organisation (spec 032, US2): add,
/// edit and remove. Contacts are listed through <see cref="ExternalOrganisation.Contacts"/>, loaded
/// by <see cref="IExternalOrganisationService"/>. Every write is audited (FR-014).
/// </summary>
public interface IExternalOrganisationContactService
{
    /// <summary>
    /// Adds a contact. Throws EntityNotFoundException for an unknown organisation, and
    /// ValidationException on a rule failure or an archived organisation. Audits Create.
    /// </summary>
    Task<ExternalOrganisationContact> AddAsync(Guid organisationId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edits a contact. Throws EntityNotFoundException for an unknown or removed contact, and
    /// ValidationException on a rule failure or an archived organisation. Audits Update.
    /// </summary>
    Task UpdateAsync(Guid contactId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Soft delete (FR-009). Throws EntityNotFoundException for an unknown or removed contact, and
    /// ValidationException for an archived organisation. Audits Delete.
    /// </summary>
    Task RemoveAsync(Guid contactId, CancellationToken ct = default);
}

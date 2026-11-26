using StageFright.Core.Modules.Organisations;

namespace StageFright.Core.Contracts;

/// <summary>
/// Builds an external organisation's financial history (spec 032 US3, ui-contract §4.9).
/// </summary>
public interface IOrganisationHistoryService
{
    /// <summary>
    /// Linked expense and income entries, newest first, with total paid and total received.
    /// Works for archived organisations. An unknown id has no linked entries, so it returns an
    /// empty history with zero totals.
    /// </summary>
    Task<OrganisationHistory> GetHistoryAsync(Guid organisationId, CancellationToken ct = default);
}

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// An organisation's financial history (spec 032 FR-011): its linked entries newest first, plus
/// the total paid to it (expense payments) and the total received from it (income entries).
/// Read model only, never persisted.
/// </summary>
public sealed record OrganisationHistory(
    IReadOnlyList<OrganisationHistoryEntry> Entries,
    decimal TotalPaid,
    decimal TotalReceived);

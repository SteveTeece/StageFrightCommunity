using StageFright.Core.Modules.Agm;

namespace StageFright.Core.Contracts;

/// <summary>Assembles the printable AGM attendance report.</summary>
public interface IAgmAttendanceSheetService
{
    /// <summary>
    /// Assembles the printable AGM attendance report. For a recorded AGM, from its fixed,
    /// already-persisted attendance roster (FR-005), sorted by surname then first name (FR-006).
    /// For a still-scheduled AGM, from every currently-active member (FR-010), also sorted by
    /// surname then first name, each with an unchecked box. Read-only — creates, updates, or
    /// deletes nothing.
    /// </summary>
    /// <exception cref="Exceptions.EntityNotFoundException">agmId does not match a saved AGM.</exception>
    Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default);
}

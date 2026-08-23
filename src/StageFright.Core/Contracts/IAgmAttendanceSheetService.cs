using StageFright.Core.Modules.Agm;

namespace StageFright.Core.Contracts;

/// <summary>Assembles the printable AGM attendance report.</summary>
public interface IAgmAttendanceSheetService
{
    /// <summary>
    /// Assembles the printable AGM attendance report from the AGM's fixed, already-persisted
    /// attendance roster (FR-005), sorted by surname then first name (FR-006). Read-only —
    /// creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="Exceptions.EntityNotFoundException">agmId does not match a saved AGM.</exception>
    Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default);
}

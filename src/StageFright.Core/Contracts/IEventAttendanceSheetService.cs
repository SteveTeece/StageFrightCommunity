using StageFright.Core.Modules.Events;

namespace StageFright.Core.Contracts;

/// <summary>Assembles the printable event attendance sheet.</summary>
public interface IEventAttendanceSheetService
{
    /// <summary>
    /// Assembles the printable event attendance sheet: every member active as of the event's
    /// date (FR-002), sorted by surname then first name (FR-006), each with a pre-computed
    /// Participated flag (FR-003) reflecting any participation already recorded. Read-only —
    /// creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="Exceptions.EntityNotFoundException">eventId does not match a saved event.</exception>
    Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default);
}

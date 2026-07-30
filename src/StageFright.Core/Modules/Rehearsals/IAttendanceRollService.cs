namespace StageFright.Core.Modules.Rehearsals;

/// <summary>Assembles the printable attendance roll for a scheduled rehearsal.</summary>
public interface IAttendanceRollService
{
    /// <summary>
    /// Assembles the printable attendance roll for a scheduled rehearsal: every member active as
    /// of the rehearsal's date, sorted by surname then first name, each with pre-computed
    /// Attended and RehearsalFeePaid flags reflecting any attendance already recorded.
    /// Read-only — creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="Exceptions.EntityNotFoundException">rehearsalId does not match a saved rehearsal.</exception>
    Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default);
}

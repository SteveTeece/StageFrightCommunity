namespace StageFright.Core.Modules.Rehearsals;

/// <summary>Assembles the printable attendance roll for a scheduled rehearsal.</summary>
public interface IAttendanceRollService
{
    /// <summary>
    /// Assembles the printable attendance roll for a scheduled rehearsal: every currently-active
    /// member, sorted by surname then first name, each with a pre-computed Annual Fee Paid flag.
    /// Read-only — creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="Exceptions.EntityNotFoundException">rehearsalId does not match a saved rehearsal.</exception>
    Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default);
}

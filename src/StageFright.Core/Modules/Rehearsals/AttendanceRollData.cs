namespace StageFright.Core.Modules.Rehearsals;

/// <summary>Printable attendance roll for a single rehearsal: rehearsal identity plus ordered member rows.</summary>
public sealed class AttendanceRollData
{
    public DateTime RehearsalDate { get; init; }
    public TimeSpan RehearsalTime { get; init; }
    public IReadOnlyList<AttendanceRollMember> Members { get; init; } = Array.Empty<AttendanceRollMember>();
}

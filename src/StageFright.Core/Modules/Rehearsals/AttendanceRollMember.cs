namespace StageFright.Core.Modules.Rehearsals;

/// <summary>One row on a printable attendance roll.</summary>
public sealed class AttendanceRollMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    /// <summary>True if this member has a recorded attendance entry marking them present at the rehearsal; false when not yet recorded or recorded absent.</summary>
    public bool Attended { get; init; }

    /// <summary>True if this member's attendance fee for the rehearsal has been recorded and has no outstanding balance; independent of <see cref="Attended"/>.</summary>
    public bool RehearsalFeePaid { get; init; }
}

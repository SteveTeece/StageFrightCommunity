namespace StageFright.Core.Modules.Events;

/// <summary>One row on a printable event attendance sheet.</summary>
public sealed class EventAttendanceSheetMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    /// <summary>True only if a ParticipationRecord exists for this member and event with Participated == true; false when not yet recorded or recorded as not participating.</summary>
    public bool Participated { get; init; }
}

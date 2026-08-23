namespace StageFright.Core.Modules.Agm;

/// <summary>One row on a printable AGM attendance report.</summary>
public sealed class AgmAttendanceSheetMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    /// <summary>Copied directly from the corresponding AgmAttendanceRecord.Attended — never recomputed.</summary>
    public bool Attended { get; init; }
}

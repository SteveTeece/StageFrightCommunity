namespace StageFright.Core.Modules.Agm;

/// <summary>Printable attendance report for a single AGM: meeting identity plus ordered member rows.</summary>
public sealed class AgmAttendanceSheetData
{
    public DateTime AgmDate { get; init; }
    public IReadOnlyList<AgmAttendanceSheetMember> Members { get; init; } = Array.Empty<AgmAttendanceSheetMember>();
}

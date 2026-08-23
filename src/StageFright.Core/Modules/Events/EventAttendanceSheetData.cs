namespace StageFright.Core.Modules.Events;

/// <summary>Printable attendance sheet for a single event: event identity plus ordered member rows.</summary>
public sealed class EventAttendanceSheetData
{
    public DateTime EventDate { get; init; }
    public string EventTypeName { get; init; } = string.Empty;
    public IReadOnlyList<EventAttendanceSheetMember> Members { get; init; } = Array.Empty<EventAttendanceSheetMember>();
}

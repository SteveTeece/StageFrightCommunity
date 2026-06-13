namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Input model for scheduling a new rehearsal.
/// </summary>
public record ScheduleRehearsalRequest
{
    public DateTime Date { get; init; }
    public TimeSpan Time { get; init; }
    public string? Notes { get; init; }
}

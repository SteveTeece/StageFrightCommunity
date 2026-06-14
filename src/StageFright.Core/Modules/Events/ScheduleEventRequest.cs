namespace StageFright.Core.Modules.Events;

/// <summary>Input model for scheduling a new event.</summary>
public record ScheduleEventRequest
{
    public DateTime Date { get; init; }
    public Guid EventTypeId { get; init; }
    public string? Notes { get; init; }
}

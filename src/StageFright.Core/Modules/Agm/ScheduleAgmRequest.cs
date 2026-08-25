namespace StageFright.Core.Modules.Agm;

/// <summary>Request to schedule an AGM ahead of time: meeting date and optional notes only.</summary>
public record ScheduleAgmRequest(DateTime Date, string? Notes);

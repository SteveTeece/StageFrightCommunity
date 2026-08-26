namespace StageFright.Core.Modules.Agm;

/// <summary>Printable results report for a single AGM: meeting date, attendance count, and every elected position.</summary>
public sealed class AgmResultsData
{
    public DateTime AgmDate { get; init; }
    public int AttendedCount { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<AgmResultsPositionLine> PositionLines { get; init; } = Array.Empty<AgmResultsPositionLine>();
    public IReadOnlyList<string> GeneralCommitteeMemberNames { get; init; } = Array.Empty<string>();
}

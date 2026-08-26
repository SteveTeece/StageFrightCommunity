namespace StageFright.Core.Modules.Agm;

/// <summary>One named office-holder position line on a printable AGM results report.</summary>
public sealed class AgmResultsPositionLine
{
    public string Label { get; init; } = string.Empty;
    public string MemberText { get; init; } = string.Empty;
}

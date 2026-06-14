namespace StageFright.Core.Modules.Events;

/// <summary>A single member's participation entry in a batch submission.</summary>
public record ParticipationBatchItem
{
    public Guid MemberId { get; init; }

    /// <summary>True if the member participated in the event.</summary>
    public bool Participated { get; init; }
}

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// A single member's attendance entry in a batch submission.
/// </summary>
public record AttendanceBatchItem
{
    public Guid MemberId { get; init; }

    /// <summary>True if the member attended the rehearsal.</summary>
    public bool Attended { get; init; }

    /// <summary>
    /// When true, the attendance fee is created with PaidAtCreation=false
    /// and no GL payment pair or Payment record is auto-created.
    /// Default false (fee is treated as paid immediately in cash).
    /// </summary>
    public bool MarkAsUnpaid { get; init; }
}

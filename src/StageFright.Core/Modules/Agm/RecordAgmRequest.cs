namespace StageFright.Core.Modules.Agm;

/// <summary>Request to record a complete AGM: meeting details, attendance, and every election.</summary>
public record RecordAgmRequest(
    DateTime Date,
    string? Notes,
    IReadOnlyList<Guid> AttendedMemberIds,
    IReadOnlyList<Guid> AllActiveMemberIds,
    IReadOnlyDictionary<Guid, Guid> OfficeHolderAssignments,
    IReadOnlyList<Guid> GeneralCommitteeMemberIds);

namespace StageFright.Core.Modules.Agm;

/// <summary>Request to record attendance and every election against an already-scheduled AGM.</summary>
public record RecordAgmRequest(
    IReadOnlyList<Guid> AttendedMemberIds,
    IReadOnlyList<Guid> AllActiveMemberIds,
    IReadOnlyDictionary<Guid, Guid> OfficeHolderAssignments,
    IReadOnlyList<Guid> GeneralCommitteeMemberIds);

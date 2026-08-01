namespace StageFright.Core.Modules.Agm;

/// <summary>Request to record a mid-term replacement (special election) against an open committee term.</summary>
public record RecordSpecialElectionRequest(
    Guid OutgoingPositionRecordId,
    Guid IncomingMemberId,
    DateTime ReplacementDate);

using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the AnnualGeneralMeeting entity 1:1.</summary>
[ProtoContract]
public class AnnualGeneralMeetingBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public string? Notes { get; set; }
    [ProtoMember(4)] public int? GeneralCommitteeSeatCountTarget { get; set; }
    [ProtoMember(5)] public bool IsDeleted { get; set; }
    [ProtoMember(6)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(7)] public string? DeletedBy { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
    [ProtoMember(9)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(10)] public bool IsRecorded { get; set; }
}

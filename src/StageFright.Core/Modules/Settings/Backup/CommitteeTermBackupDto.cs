using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the CommitteeTerm entity 1:1.</summary>
[ProtoContract]
public class CommitteeTermBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid StartedByAgmId { get; set; }
    [ProtoMember(3)] public DateTime StartDate { get; set; }
    [ProtoMember(4)] public DateTime? EndDate { get; set; }
    [ProtoMember(5)] public int LabelYear { get; set; }
    [ProtoMember(6)] public DateTime CreatedAt { get; set; }
    [ProtoMember(7)] public DateTime UpdatedAt { get; set; }
}

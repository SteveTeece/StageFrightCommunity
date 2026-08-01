using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the CommitteePositionRecord entity 1:1.</summary>
[ProtoContract]
public class CommitteePositionRecordBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid MemberId { get; set; }
    [ProtoMember(3)] public int? Year { get; set; }
    [ProtoMember(4)] public string? Position { get; set; }
    [ProtoMember(5)] public bool IsDeleted { get; set; }
    [ProtoMember(6)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(7)] public string? DeletedBy { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
    [ProtoMember(9)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(10)] public Guid? CommitteeTermId { get; set; }
    [ProtoMember(11)] public Guid? OfficeHolderTypeId { get; set; }
    [ProtoMember(12)] public DateTime? StartDate { get; set; }
    [ProtoMember(13)] public DateTime? EndDate { get; set; }
}

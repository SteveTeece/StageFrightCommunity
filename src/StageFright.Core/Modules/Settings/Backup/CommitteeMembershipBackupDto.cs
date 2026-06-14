using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the CommitteeMembership entity 1:1.</summary>
[ProtoContract]
public class CommitteeMembershipBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid MemberId { get; set; }
    [ProtoMember(3)] public int Year { get; set; }
    [ProtoMember(4)] public string Position { get; set; } = string.Empty;
    [ProtoMember(5)] public bool IsDeleted { get; set; }
    [ProtoMember(6)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(7)] public string? DeletedBy { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
    [ProtoMember(9)] public DateTime UpdatedAt { get; set; }
}

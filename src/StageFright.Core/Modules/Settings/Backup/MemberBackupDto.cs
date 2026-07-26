using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Member entity 1:1.</summary>
[ProtoContract]
public class MemberBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string LegacyName { get; set; } = string.Empty;
    [ProtoMember(3)] public string StreetAddress { get; set; } = string.Empty;
    [ProtoMember(4)] public string? Phone { get; set; }
    [ProtoMember(5)] public string? Email { get; set; }
    [ProtoMember(6)] public DateTime JoinDate { get; set; }
    [ProtoMember(7)] public DateTime? DateOfBirth { get; set; }
    [ProtoMember(8)] public MemberStatus Status { get; set; }
    [ProtoMember(9)] public DateTime? ActivateDate { get; set; }
    [ProtoMember(10)] public DateTime? InactivateDate { get; set; }
    [ProtoMember(11)] public bool IsDeleted { get; set; }
    [ProtoMember(12)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(13)] public string? DeletedBy { get; set; }
    [ProtoMember(14)] public DateTime CreatedAt { get; set; }
    [ProtoMember(15)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(16)] public string FirstName { get; set; } = string.Empty;
    [ProtoMember(17)] public string LastName { get; set; } = string.Empty;
}

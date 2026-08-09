using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Settings singleton entity 1:1.</summary>
[ProtoContract]
public class SettingsBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string OrganizationName { get; set; } = string.Empty;
    [ProtoMember(3)] public decimal AnnualFee { get; set; }
    [ProtoMember(4)] public decimal AttendanceFee { get; set; }
    [ProtoMember(5)] public int MembershipRenewalMonth { get; set; }
    [ProtoMember(6)] public int CommitteeRenewalMonth { get; set; }
    [ProtoMember(7)] public int MaxAgeRangeYears { get; set; }
    [ProtoMember(8)] public int MinimumMemberAge { get; set; }
    [ProtoMember(9)] public Theme Theme { get; set; }
    [ProtoMember(11)] public string SchemaVersion { get; set; } = "1.0.0";
    [ProtoMember(12)] public bool IsDeleted { get; set; }
    [ProtoMember(13)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(14)] public string? DeletedBy { get; set; }
    [ProtoMember(15)] public DateTime CreatedAt { get; set; }
    [ProtoMember(16)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(17)] public int? GeneralCommitteeSeatCountTarget { get; set; }
}

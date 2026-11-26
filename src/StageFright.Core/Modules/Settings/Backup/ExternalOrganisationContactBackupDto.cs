using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the ExternalOrganisationContact entity 1:1. Added in schema 1.3.0.</summary>
[ProtoContract]
public class ExternalOrganisationContactBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid ExternalOrganisationId { get; set; }
    [ProtoMember(3)] public string Name { get; set; } = string.Empty;
    [ProtoMember(4)] public string? Role { get; set; }
    [ProtoMember(5)] public string? Phone { get; set; }
    [ProtoMember(6)] public string? Mobile { get; set; }
    [ProtoMember(7)] public string? Email { get; set; }
    [ProtoMember(8)] public bool IsDeleted { get; set; }
    [ProtoMember(9)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(10)] public string? DeletedBy { get; set; }
    [ProtoMember(11)] public DateTime CreatedAt { get; set; }
    [ProtoMember(12)] public DateTime UpdatedAt { get; set; }
}

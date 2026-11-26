using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the ExternalOrganisation entity 1:1. Added in schema 1.3.0.</summary>
[ProtoContract]
public class ExternalOrganisationBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public string? Address { get; set; }
    [ProtoMember(4)] public string? Email { get; set; }
    [ProtoMember(5)] public bool IsDeleted { get; set; }
    [ProtoMember(6)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(7)] public string? DeletedBy { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
    [ProtoMember(9)] public DateTime UpdatedAt { get; set; }
}

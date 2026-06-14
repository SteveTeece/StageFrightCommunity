using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the AuditTrailEntry entity 1:1.</summary>
[ProtoContract]
public class AuditTrailBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(3)] public Guid EntityId { get; set; }
    [ProtoMember(4)] public AuditAction Action { get; set; }
    [ProtoMember(5)] public string? OldValue { get; set; }
    [ProtoMember(6)] public string? NewValue { get; set; }
    [ProtoMember(7)] public string UserId { get; set; } = "system";
    [ProtoMember(8)] public DateTime Timestamp { get; set; }
}

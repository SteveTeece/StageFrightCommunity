using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the EventType entity 1:1. Nested inside EventBackupDto.</summary>
[ProtoContract]
public class EventTypeBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public bool IsSystemDefault { get; set; }
    [ProtoMember(4)] public bool IsDeleted { get; set; }
    [ProtoMember(5)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(6)] public string? DeletedBy { get; set; }
    [ProtoMember(7)] public DateTime CreatedAt { get; set; }
    [ProtoMember(8)] public DateTime UpdatedAt { get; set; }
}

using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the ReconciliationLine entity 1:1. No soft-delete fields (follows its parent).</summary>
[ProtoContract]
public class ReconciliationLineBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid ReconciliationId { get; set; }
    [ProtoMember(3)] public Guid TransactionId { get; set; }
    [ProtoMember(4)] public DateTime CreatedAt { get; set; }
}

using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Fee entity 1:1. No soft-delete fields (financial exemption).</summary>
[ProtoContract]
public class FeeBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid MemberId { get; set; }
    [ProtoMember(3)] public FeeType FeeType { get; set; }
    [ProtoMember(4)] public decimal Amount { get; set; }
    [ProtoMember(5)] public DateTime FeeDate { get; set; }
    [ProtoMember(6)] public DateTime DueDate { get; set; }
    [ProtoMember(7)] public bool PaidAtCreation { get; set; }
    [ProtoMember(8)] public Guid? RehearsalId { get; set; }
    [ProtoMember(9)] public DateTime CreatedAt { get; set; }
}

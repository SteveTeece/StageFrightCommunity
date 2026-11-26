using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the BankDepositLine entity 1:1. No soft-delete fields (financial exemption).
/// Added in schema 1.4.0 (spec 033).
/// </summary>
[ProtoContract]
public class BankDepositLineBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid BankDepositId { get; set; }
    [ProtoMember(3)] public Guid ReceivedItemId { get; set; }
    [ProtoMember(4)] public DateTime CreatedAt { get; set; }
}

using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Transaction entity 1:1. No soft-delete fields (financial exemption).</summary>
[ProtoContract]
public class TransactionBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public Guid CategoryId { get; set; }
    [ProtoMember(4)] public decimal DebitAmount { get; set; }
    [ProtoMember(5)] public decimal CreditAmount { get; set; }
    [ProtoMember(6)] public string GLAccount { get; set; } = string.Empty;
    [ProtoMember(7)] public Guid? MemberId { get; set; }
    [ProtoMember(8)] public Guid? PaymentId { get; set; }
    [ProtoMember(9)] public Guid? FeeId { get; set; }
    [ProtoMember(10)] public string? Description { get; set; }
    [ProtoMember(11)] public DateTime CreatedAt { get; set; }
}

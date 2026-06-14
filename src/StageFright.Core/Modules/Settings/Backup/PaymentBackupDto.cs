using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Payment entity 1:1. No soft-delete fields (financial exemption).</summary>
[ProtoContract]
public class PaymentBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid MemberId { get; set; }
    [ProtoMember(3)] public DateTime Date { get; set; }
    [ProtoMember(4)] public decimal Amount { get; set; }
    [ProtoMember(5)] public PaymentMethod PaymentMethod { get; set; }
    [ProtoMember(6)] public PaymentType PaymentType { get; set; }
    [ProtoMember(7)] public string? Notes { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
    [ProtoMember(9)] public DateTime UpdatedAt { get; set; }
}

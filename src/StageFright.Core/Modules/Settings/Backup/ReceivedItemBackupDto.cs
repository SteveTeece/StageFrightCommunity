using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the ReceivedItem entity 1:1. No soft-delete fields (financial exemption).
/// Added in schema 1.4.0 (spec 033).
/// </summary>
[ProtoContract]
public class ReceivedItemBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public decimal Amount { get; set; }
    [ProtoMember(4)] public PaymentMethod Method { get; set; }
    [ProtoMember(5)] public string? Reference { get; set; }
    [ProtoMember(6)] public Guid? PaymentId { get; set; }
    [ProtoMember(7)] public Guid? JournalEntryId { get; set; }
    [ProtoMember(8)] public DateTime CreatedAt { get; set; }
}

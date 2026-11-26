using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the BankDeposit entity 1:1. No soft-delete fields (financial exemption).
/// Added in schema 1.4.0 (spec 033).
/// </summary>
[ProtoContract]
public class BankDepositBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public Guid BankAccountId { get; set; }
    [ProtoMember(4)] public decimal CashAmount { get; set; }
    [ProtoMember(5)] public decimal ChequeTotal { get; set; }
    [ProtoMember(6)] public Guid JournalEntryId { get; set; }
    [ProtoMember(7)] public Guid? ReversalJournalEntryId { get; set; }
    [ProtoMember(8)] public DateTime? ReversedAt { get; set; }
    [ProtoMember(9)] public DateTime CreatedAt { get; set; }
}

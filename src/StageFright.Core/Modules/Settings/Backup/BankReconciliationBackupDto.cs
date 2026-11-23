using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the BankReconciliation entity 1:1.</summary>
[ProtoContract]
public class BankReconciliationBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid AccountId { get; set; }
    [ProtoMember(3)] public DateTime StatementDate { get; set; }
    [ProtoMember(4)] public decimal StatementClosingBalance { get; set; }
    [ProtoMember(5)] public decimal OpeningBalance { get; set; }
    [ProtoMember(6)] public ReconciliationStatus Status { get; set; }
    [ProtoMember(7)] public DateTime? FinalisedAt { get; set; }
    [ProtoMember(8)] public string? Notes { get; set; }
    [ProtoMember(9)] public bool IsDeleted { get; set; }
    [ProtoMember(10)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(11)] public string? DeletedBy { get; set; }
    [ProtoMember(12)] public DateTime CreatedAt { get; set; }
    [ProtoMember(13)] public DateTime UpdatedAt { get; set; }
}

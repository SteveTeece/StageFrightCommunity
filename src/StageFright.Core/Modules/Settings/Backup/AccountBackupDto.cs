using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the Account entity 1:1 (formerly CategoryBackupDto — the
/// protobuf field numbers are unchanged, so pre-expansion backups still deserialize;
/// field 4 was GLAccount and now carries AccountNumber, field 12 is new).
/// </summary>
[ProtoContract]
public class AccountBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public AccountType Type { get; set; }
    [ProtoMember(4)] public string AccountNumber { get; set; } = string.Empty;
    [ProtoMember(5)] public int SortOrder { get; set; }
    [ProtoMember(6)] public bool IsSystem { get; set; }
    [ProtoMember(7)] public bool IsDeleted { get; set; }
    [ProtoMember(8)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(9)] public string? DeletedBy { get; set; }
    [ProtoMember(10)] public DateTime CreatedAt { get; set; }
    [ProtoMember(11)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(12)] public bool IsBankAccount { get; set; }
}

using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the JournalEntry entity 1:1. No soft-delete fields (immutable GL header).</summary>
[ProtoContract]
public class JournalEntryBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public JournalEntryType Type { get; set; }
    [ProtoMember(3)] public DateTime Date { get; set; }
    [ProtoMember(4)] public string? Description { get; set; }
    [ProtoMember(5)] public DateTime CreatedAt { get; set; }
}

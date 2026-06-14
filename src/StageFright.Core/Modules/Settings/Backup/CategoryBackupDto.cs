using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Category entity 1:1.</summary>
[ProtoContract]
public class CategoryBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public CategoryType Type { get; set; }
    [ProtoMember(4)] public string GLAccount { get; set; } = string.Empty;
    [ProtoMember(5)] public int SortOrder { get; set; }
    [ProtoMember(6)] public bool IsSystem { get; set; }
    [ProtoMember(7)] public bool IsDeleted { get; set; }
    [ProtoMember(8)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(9)] public string? DeletedBy { get; set; }
    [ProtoMember(10)] public DateTime CreatedAt { get; set; }
    [ProtoMember(11)] public DateTime UpdatedAt { get; set; }
}

using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the AttendanceRecord entity 1:1. Nested inside RehearsalBackupDto.</summary>
[ProtoContract]
public class AttendanceRecordBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public Guid RehearsalId { get; set; }
    [ProtoMember(3)] public Guid MemberId { get; set; }
    [ProtoMember(4)] public bool Attended { get; set; }
    [ProtoMember(5)] public DateTime CreatedAt { get; set; }
    [ProtoMember(6)] public bool IsDeleted { get; set; }
    [ProtoMember(7)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(8)] public string? DeletedBy { get; set; }
}

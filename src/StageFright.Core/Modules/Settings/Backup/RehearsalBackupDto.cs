using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the Rehearsal entity 1:1, with nested AttendanceRecords.
/// Nesting ensures each rehearsal and its attendance records are kept together in the backup.
/// </summary>
[ProtoContract]
public class RehearsalBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public TimeSpan Time { get; set; }
    [ProtoMember(4)] public string? Notes { get; set; }
    [ProtoMember(5)] public decimal? StoredAttendanceRate { get; set; }
    [ProtoMember(6)] public bool IsDeleted { get; set; }
    [ProtoMember(7)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(8)] public string? DeletedBy { get; set; }
    [ProtoMember(9)] public DateTime CreatedAt { get; set; }
    [ProtoMember(10)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(11)] public List<AttendanceRecordBackupDto> AttendanceRecords { get; set; } = new();
}

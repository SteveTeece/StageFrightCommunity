using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Backup DTO mirroring the Event entity 1:1, with nested EventType and ParticipationRecords.
/// Including the EventType inline ensures the FK reference can be restored without a separate collection.
/// </summary>
[ProtoContract]
public class EventBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public DateTime Date { get; set; }
    [ProtoMember(3)] public Guid EventTypeId { get; set; }
    [ProtoMember(4)] public string? Notes { get; set; }
    [ProtoMember(5)] public decimal? StoredParticipationRate { get; set; }
    [ProtoMember(6)] public bool IsDeleted { get; set; }
    [ProtoMember(7)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(8)] public string? DeletedBy { get; set; }
    [ProtoMember(9)] public DateTime CreatedAt { get; set; }
    [ProtoMember(10)] public DateTime UpdatedAt { get; set; }

    /// <summary>EventType snapshot at backup time. Upserted before the Event during import.</summary>
    [ProtoMember(11)] public EventTypeBackupDto? EventType { get; set; }

    [ProtoMember(12)] public List<ParticipationRecordBackupDto> ParticipationRecords { get; set; } = new();
}

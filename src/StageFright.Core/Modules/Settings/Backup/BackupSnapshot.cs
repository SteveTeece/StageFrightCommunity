using StageFright.Core.Entities;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// A complete in-memory snapshot of all entity data, used as an intermediary between
/// IBackupRepository (which reads/writes entities) and BackupService (which maps to/from DTOs).
/// </summary>
public sealed class BackupSnapshot
{
    public IReadOnlyList<Member> Members { get; init; } = Array.Empty<Member>();
    public IReadOnlyList<CommitteeMembership> CommitteeMemberships { get; init; } = Array.Empty<CommitteeMembership>();
    public IReadOnlyList<Rehearsal> Rehearsals { get; init; } = Array.Empty<Rehearsal>();
    public IReadOnlyList<AttendanceRecord> AttendanceRecords { get; init; } = Array.Empty<AttendanceRecord>();
    public IReadOnlyList<Event> Events { get; init; } = Array.Empty<Event>();
    public IReadOnlyList<EventType> EventTypes { get; init; } = Array.Empty<EventType>();
    public IReadOnlyList<ParticipationRecord> ParticipationRecords { get; init; } = Array.Empty<ParticipationRecord>();
    public IReadOnlyList<Fee> Fees { get; init; } = Array.Empty<Fee>();
    public IReadOnlyList<Payment> Payments { get; init; } = Array.Empty<Payment>();
    public IReadOnlyList<Transaction> Transactions { get; init; } = Array.Empty<Transaction>();
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    public SettingsEntity? Settings { get; init; }
    public IReadOnlyList<AuditTrailEntry> AuditTrailEntries { get; init; } = Array.Empty<AuditTrailEntry>();
}

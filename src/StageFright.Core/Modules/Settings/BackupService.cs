using Microsoft.Extensions.Logging;
using ProtoBuf;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings.Backup;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Backup and restore service. Export writes all entity data (including soft-deleted) to a
/// protobuf binary .sfbak file. Import validates, checkpoints current data, then atomically
/// upserts every record from the backup file.
/// </summary>
public class BackupService : IBackupService
{
    private const string SupportedMajorVersion = "1";

    private readonly IBackupRepository _backupRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrailService _audit;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IBackupRepository backupRepo,
        IUnitOfWork uow,
        IAuditTrailService audit,
        ILogger<BackupService> logger)
    {
        _backupRepo = backupRepo;
        _uow = uow;
        _audit = audit;
        _logger = logger;
    }

    public async Task ExportAsync(string filePath, CancellationToken ct = default)
    {
        var snapshot = await _backupRepo.GetFullSnapshotAsync(ct);
        var envelope = MapToEnvelope(snapshot);

        try
        {
            using var stream = File.Create(filePath);
            Serializer.Serialize(stream, envelope);
        }
        catch (Exception ex) when (ex is not ImportException)
        {
            throw new DataAccessException($"Failed to write backup file: {ex.Message}", "Backup", nameof(ExportAsync), null, ex);
        }

        _logger.LogInformation(
            "Backup exported to {FilePath}. Counts: {Counts}",
            filePath,
            string.Join(", ", envelope.EntityCounts.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public async Task ImportAsync(string filePath, CancellationToken ct = default)
    {
        var envelope = DeserializeAndValidate(filePath);

        // Pre-import checkpoint: export current DB before any write
        var checkpointPath = GenerateCheckpointPath(filePath);
        await ExportAsync(checkpointPath, ct);
        _logger.LogInformation("Pre-import checkpoint saved to {CheckpointPath}", checkpointPath);

        var snapshot = MapToSnapshot(envelope);

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _backupRepo.UpsertSnapshotAsync(snapshot, innerCt);
        }, ct);

        await _audit.LogAsync(
            entityType: "Backup",
            entityId: Guid.Empty,
            action: AuditAction.Import,
            newValue: $"Imported from {Path.GetFileName(filePath)}; checkpoint: {checkpointPath}",
            ct: ct);

        _logger.LogInformation(
            "Import completed from {FilePath}. Checkpoint at {CheckpointPath}. Counts: {Counts}",
            filePath,
            checkpointPath,
            string.Join(", ", envelope.EntityCounts.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public Task<BackupManifest> GetManifestAsync(string filePath, CancellationToken ct = default)
    {
        var envelope = DeserializeAndValidate(filePath);
        return Task.FromResult(new BackupManifest
        {
            SchemaVersion = envelope.SchemaVersion,
            GeneratedAt = envelope.GeneratedAt,
            ApplicationVersion = envelope.ApplicationVersion,
            EntityCounts = envelope.EntityCounts
        });
    }

    // --- Private helpers ---

    private static BackupEnvelope DeserializeAndValidate(string filePath)
    {
        BackupEnvelope envelope;
        try
        {
            using var stream = File.OpenRead(filePath);
            envelope = Serializer.Deserialize<BackupEnvelope>(stream);
        }
        catch (Exception ex)
        {
            throw new ImportException(
                "Backup file is corrupted or not a StageFright backup.",
                innerException: ex);
        }

        // protobuf-net serializes empty List<T> as absent (no bytes), so empty collections
        // deserialize as null. Initialize them to empty so they are safe to iterate.
        // Presence is determined via EntityCounts, which always includes every key even at 0.
        envelope.Members ??= [];
        envelope.CommitteeMemberships ??= [];
        envelope.Rehearsals ??= [];
        envelope.Events ??= [];
        envelope.Fees ??= [];
        envelope.Payments ??= [];
        envelope.Transactions ??= [];
        envelope.Categories ??= [];
        envelope.AuditTrailEntries ??= [];
        envelope.EntityCounts ??= new Dictionary<string, int>();

        ValidateVersion(envelope.SchemaVersion);
        ValidateCompleteness(envelope);
        return envelope;
    }

    private static void ValidateVersion(string schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new ImportException(
                "Backup file is missing a schema version.",
                operationContext: "VersionCheck");

        var majorStr = schemaVersion.Split('.')[0];
        if (majorStr != SupportedMajorVersion)
            throw new ImportException(
                $"This backup uses schema version {schemaVersion}; this application supports major version {SupportedMajorVersion}. Please upgrade the application to restore this backup.",
                operationContext: "VersionCheck");
    }

    private static void ValidateCompleteness(BackupEnvelope envelope)
    {
        // EntityCounts is the canonical presence marker. MapToEnvelope always writes every key
        // (even with count 0), so a missing key means the collection was never exported — the
        // file is from an incompatible or truncated format.
        var counts = envelope.EntityCounts;
        if (!counts.ContainsKey("Members")) Fail("Members");
        if (!counts.ContainsKey("CommitteeMemberships")) Fail("CommitteeMemberships");
        if (!counts.ContainsKey("Rehearsals")) Fail("Rehearsals");
        if (!counts.ContainsKey("Events")) Fail("Events");
        if (!counts.ContainsKey("Fees")) Fail("Fees");
        if (!counts.ContainsKey("Payments")) Fail("Payments");
        if (!counts.ContainsKey("Transactions")) Fail("Transactions");
        if (!counts.ContainsKey("Categories")) Fail("Categories");
        if (!counts.ContainsKey("AuditTrailEntries")) Fail("AuditTrailEntries");

        static void Fail(string entityType) =>
            throw new ImportException(
                $"Import file incomplete: missing {entityType}. Restore from complete backup.",
                entityType: entityType,
                operationContext: "CompletenessCheck");
    }

    private static string GenerateCheckpointPath(string importFilePath)
    {
        var dir = Path.GetDirectoryName(importFilePath);
        if (string.IsNullOrEmpty(dir))
            dir = Path.GetTempPath();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(dir, $"StageFright-Checkpoint-{timestamp}.sfbak");
    }

    private static BackupEnvelope MapToEnvelope(BackupSnapshot snapshot)
    {
        var members = snapshot.Members.Select(MapMember).ToList();
        var committees = snapshot.CommitteeMemberships.Select(MapCommittee).ToList();
        var rehearsals = snapshot.Rehearsals.Select(r => MapRehearsal(r, snapshot.AttendanceRecords)).ToList();
        var eventTypeMap = snapshot.EventTypes.ToDictionary(et => et.Id);
        var participationMap = snapshot.ParticipationRecords.ToLookup(pr => pr.EventId);
        var events = snapshot.Events.Select(e => MapEvent(e, eventTypeMap, participationMap)).ToList();
        var fees = snapshot.Fees.Select(MapFee).ToList();
        var payments = snapshot.Payments.Select(MapPayment).ToList();
        var transactions = snapshot.Transactions.Select(MapTransaction).ToList();
        var categories = snapshot.Categories.Select(MapCategory).ToList();
        var settings = snapshot.Settings is not null ? MapSettings(snapshot.Settings) : null;
        var auditEntries = snapshot.AuditTrailEntries.Select(MapAuditEntry).ToList();

        var counts = new Dictionary<string, int>
        {
            ["Members"] = members.Count,
            ["CommitteeMemberships"] = committees.Count,
            ["Rehearsals"] = rehearsals.Count,
            ["AttendanceRecords"] = rehearsals.Sum(r => r.AttendanceRecords.Count),
            ["Events"] = events.Count,
            ["EventTypes"] = eventTypeMap.Count,
            ["ParticipationRecords"] = events.Sum(e => e.ParticipationRecords.Count),
            ["Fees"] = fees.Count,
            ["Payments"] = payments.Count,
            ["Transactions"] = transactions.Count,
            ["Categories"] = categories.Count,
            ["Settings"] = settings is null ? 0 : 1,
            ["AuditTrailEntries"] = auditEntries.Count
        };

        return new BackupEnvelope
        {
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            ApplicationVersion = "1.0.0",
            Members = members,
            CommitteeMemberships = committees,
            Rehearsals = rehearsals,
            Events = events,
            Fees = fees,
            Payments = payments,
            Transactions = transactions,
            Categories = categories,
            Settings = settings,
            AuditTrailEntries = auditEntries,
            EntityCounts = counts
        };
    }

    private static BackupSnapshot MapToSnapshot(BackupEnvelope env)
    {
        var attendanceRecords = env.Rehearsals!
            .SelectMany(r => r.AttendanceRecords.Select(a => MapAttendanceRecord(a)))
            .ToList();

        var eventTypes = env.Events!
            .Where(e => e.EventType is not null)
            .Select(e => e.EventType!)
            .DistinctBy(et => et.Id)
            .Select(MapEventType)
            .ToList();

        var participationRecords = env.Events!
            .SelectMany(e => e.ParticipationRecords.Select(pr => MapParticipationRecord(pr)))
            .ToList();

        return new BackupSnapshot
        {
            Members = env.Members!.Select(MapMemberFromDto).ToList(),
            CommitteeMemberships = env.CommitteeMemberships!.Select(MapCommitteeFromDto).ToList(),
            Rehearsals = env.Rehearsals!.Select(MapRehearsalFromDto).ToList(),
            AttendanceRecords = attendanceRecords,
            Events = env.Events!.Select(MapEventFromDto).ToList(),
            EventTypes = eventTypes,
            ParticipationRecords = participationRecords,
            Fees = env.Fees!.Select(MapFeeFromDto).ToList(),
            Payments = env.Payments!.Select(MapPaymentFromDto).ToList(),
            Transactions = env.Transactions!.Select(MapTransactionFromDto).ToList(),
            Categories = env.Categories!.Select(MapCategoryFromDto).ToList(),
            Settings = env.Settings is not null ? MapSettingsFromDto(env.Settings) : null,
            AuditTrailEntries = env.AuditTrailEntries!.Select(MapAuditEntryFromDto).ToList()
        };
    }

    // --- Entity → DTO mappers ---

    private static MemberBackupDto MapMember(Member m) => new()
    {
        Id = m.Id, Name = m.Name, StreetAddress = m.StreetAddress,
        Phone = m.Phone, Email = m.Email, JoinDate = m.JoinDate,
        DateOfBirth = m.DateOfBirth, Status = m.Status,
        ActivateDate = m.ActivateDate, InactivateDate = m.InactivateDate,
        IsDeleted = m.IsDeleted, DeletedAt = m.DeletedAt, DeletedBy = m.DeletedBy,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };

    private static CommitteeMembershipBackupDto MapCommittee(CommitteeMembership c) => new()
    {
        Id = c.Id, MemberId = c.MemberId, Year = c.Year, Position = c.Position,
        IsDeleted = c.IsDeleted, DeletedAt = c.DeletedAt, DeletedBy = c.DeletedBy,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };

    private static RehearsalBackupDto MapRehearsal(Rehearsal r, IReadOnlyList<AttendanceRecord> allAttendance) => new()
    {
        Id = r.Id, Date = r.Date, Time = r.Time, Notes = r.Notes,
        StoredAttendanceRate = r.StoredAttendanceRate,
        IsDeleted = r.IsDeleted, DeletedAt = r.DeletedAt, DeletedBy = r.DeletedBy,
        CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt,
        AttendanceRecords = allAttendance
            .Where(a => a.RehearsalId == r.Id)
            .Select(a => new AttendanceRecordBackupDto
            {
                Id = a.Id, RehearsalId = a.RehearsalId, MemberId = a.MemberId,
                Attended = a.Attended, CreatedAt = a.CreatedAt,
                IsDeleted = a.IsDeleted, DeletedAt = a.DeletedAt, DeletedBy = a.DeletedBy
            }).ToList()
    };

    private static EventBackupDto MapEvent(Event e, Dictionary<Guid, EventType> eventTypeMap, ILookup<Guid, ParticipationRecord> participationMap)
    {
        eventTypeMap.TryGetValue(e.EventTypeId, out var eventType);
        return new EventBackupDto
        {
            Id = e.Id, Date = e.Date, EventTypeId = e.EventTypeId, Notes = e.Notes,
            StoredParticipationRate = e.StoredParticipationRate,
            IsDeleted = e.IsDeleted, DeletedAt = e.DeletedAt, DeletedBy = e.DeletedBy,
            CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
            EventType = eventType is not null ? new EventTypeBackupDto
            {
                Id = eventType.Id, Name = eventType.Name,
                IsSystemDefault = eventType.IsSystemDefault,
                IsDeleted = eventType.IsDeleted, DeletedAt = eventType.DeletedAt,
                DeletedBy = eventType.DeletedBy,
                CreatedAt = eventType.CreatedAt, UpdatedAt = eventType.UpdatedAt
            } : null,
            ParticipationRecords = participationMap[e.Id].Select(pr => new ParticipationRecordBackupDto
            {
                Id = pr.Id, EventId = pr.EventId, MemberId = pr.MemberId,
                Participated = pr.Participated, CreatedAt = pr.CreatedAt,
                IsDeleted = pr.IsDeleted, DeletedAt = pr.DeletedAt, DeletedBy = pr.DeletedBy
            }).ToList()
        };
    }

    private static FeeBackupDto MapFee(Fee f) => new()
    {
        Id = f.Id, MemberId = f.MemberId, FeeType = f.FeeType, Amount = f.Amount,
        FeeDate = f.FeeDate, DueDate = f.DueDate, PaidAtCreation = f.PaidAtCreation,
        RehearsalId = f.RehearsalId, CreatedAt = f.CreatedAt
    };

    private static PaymentBackupDto MapPayment(Payment p) => new()
    {
        Id = p.Id, MemberId = p.MemberId, Date = p.Date, Amount = p.Amount,
        PaymentMethod = p.PaymentMethod, PaymentType = p.PaymentType,
        Notes = p.Notes, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
    };

    private static TransactionBackupDto MapTransaction(Transaction t) => new()
    {
        Id = t.Id, Date = t.Date, CategoryId = t.CategoryId,
        DebitAmount = t.DebitAmount, CreditAmount = t.CreditAmount,
        GLAccount = t.GLAccount, MemberId = t.MemberId, PaymentId = t.PaymentId,
        FeeId = t.FeeId, Description = t.Description, CreatedAt = t.CreatedAt
    };

    private static CategoryBackupDto MapCategory(Category c) => new()
    {
        Id = c.Id, Name = c.Name, Type = c.Type, GLAccount = c.GLAccount,
        SortOrder = c.SortOrder, IsSystem = c.IsSystem,
        IsDeleted = c.IsDeleted, DeletedAt = c.DeletedAt, DeletedBy = c.DeletedBy,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };

    private static SettingsBackupDto MapSettings(SettingsEntity s) => new()
    {
        Id = s.Id, OrganizationName = s.OrganizationName,
        AnnualFee = s.AnnualFee, AttendanceFee = s.AttendanceFee,
        MembershipRenewalMonth = s.MembershipRenewalMonth,
        CommitteeRenewalMonth = s.CommitteeRenewalMonth,
        MaxAgeRangeYears = s.MaxAgeRangeYears, MinimumMemberAge = s.MinimumMemberAge,
        Theme = s.Theme, LastCommitteeResetYear = s.LastCommitteeResetYear,
        SchemaVersion = s.SchemaVersion,
        IsDeleted = s.IsDeleted, DeletedAt = s.DeletedAt, DeletedBy = s.DeletedBy,
        CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
    };

    private static AuditTrailBackupDto MapAuditEntry(AuditTrailEntry a) => new()
    {
        Id = a.Id, EntityType = a.EntityType, EntityId = a.EntityId,
        Action = a.Action, OldValue = a.OldValue, NewValue = a.NewValue,
        UserId = a.UserId, Timestamp = a.Timestamp
    };

    // --- DTO → Entity mappers ---

    private static Member MapMemberFromDto(MemberBackupDto d) => new()
    {
        Id = d.Id, Name = d.Name, StreetAddress = d.StreetAddress,
        Phone = d.Phone, Email = d.Email, JoinDate = d.JoinDate,
        DateOfBirth = d.DateOfBirth, Status = d.Status,
        ActivateDate = d.ActivateDate, InactivateDate = d.InactivateDate,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static CommitteeMembership MapCommitteeFromDto(CommitteeMembershipBackupDto d) => new()
    {
        Id = d.Id, MemberId = d.MemberId, Year = d.Year, Position = d.Position,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static Rehearsal MapRehearsalFromDto(RehearsalBackupDto d) => new()
    {
        Id = d.Id, Date = d.Date, Time = d.Time, Notes = d.Notes,
        StoredAttendanceRate = d.StoredAttendanceRate,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static AttendanceRecord MapAttendanceRecord(AttendanceRecordBackupDto d) => new()
    {
        Id = d.Id, RehearsalId = d.RehearsalId, MemberId = d.MemberId,
        Attended = d.Attended, CreatedAt = d.CreatedAt,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy
    };

    private static Event MapEventFromDto(EventBackupDto d) => new()
    {
        Id = d.Id, Date = d.Date, EventTypeId = d.EventTypeId, Notes = d.Notes,
        StoredParticipationRate = d.StoredParticipationRate,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static EventType MapEventType(EventTypeBackupDto d) => new()
    {
        Id = d.Id, Name = d.Name, IsSystemDefault = d.IsSystemDefault,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static ParticipationRecord MapParticipationRecord(ParticipationRecordBackupDto d) => new()
    {
        Id = d.Id, EventId = d.EventId, MemberId = d.MemberId,
        Participated = d.Participated, CreatedAt = d.CreatedAt,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy
    };

    private static Fee MapFeeFromDto(FeeBackupDto d) => new()
    {
        Id = d.Id, MemberId = d.MemberId, FeeType = d.FeeType, Amount = d.Amount,
        FeeDate = d.FeeDate, DueDate = d.DueDate, PaidAtCreation = d.PaidAtCreation,
        RehearsalId = d.RehearsalId, CreatedAt = d.CreatedAt
    };

    private static Payment MapPaymentFromDto(PaymentBackupDto d) => new()
    {
        Id = d.Id, MemberId = d.MemberId, Date = d.Date, Amount = d.Amount,
        PaymentMethod = d.PaymentMethod, PaymentType = d.PaymentType,
        Notes = d.Notes, CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static Transaction MapTransactionFromDto(TransactionBackupDto d) => new()
    {
        Id = d.Id, Date = d.Date, CategoryId = d.CategoryId,
        DebitAmount = d.DebitAmount, CreditAmount = d.CreditAmount,
        GLAccount = d.GLAccount, MemberId = d.MemberId, PaymentId = d.PaymentId,
        FeeId = d.FeeId, Description = d.Description, CreatedAt = d.CreatedAt
    };

    private static Category MapCategoryFromDto(CategoryBackupDto d) => new()
    {
        Id = d.Id, Name = d.Name, Type = d.Type, GLAccount = d.GLAccount,
        SortOrder = d.SortOrder, IsSystem = d.IsSystem,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static SettingsEntity MapSettingsFromDto(SettingsBackupDto d) => new()
    {
        Id = d.Id, OrganizationName = d.OrganizationName,
        AnnualFee = d.AnnualFee, AttendanceFee = d.AttendanceFee,
        MembershipRenewalMonth = d.MembershipRenewalMonth,
        CommitteeRenewalMonth = d.CommitteeRenewalMonth,
        MaxAgeRangeYears = d.MaxAgeRangeYears, MinimumMemberAge = d.MinimumMemberAge,
        Theme = d.Theme, LastCommitteeResetYear = d.LastCommitteeResetYear,
        SchemaVersion = d.SchemaVersion,
        IsDeleted = d.IsDeleted, DeletedAt = d.DeletedAt, DeletedBy = d.DeletedBy,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
    };

    private static AuditTrailEntry MapAuditEntryFromDto(AuditTrailBackupDto d) => new()
    {
        Id = d.Id, EntityType = d.EntityType, EntityId = d.EntityId,
        Action = d.Action, OldValue = d.OldValue, NewValue = d.NewValue,
        UserId = d.UserId, Timestamp = d.Timestamp
    };
}

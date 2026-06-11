# Contracts: Backup / Restore Format (FR-012 – FR-015)

**Serialization**: Protocol Buffers binary via **protobuf-net** (research.md R2). File extension: `.sfbak`. File name: `StageFright-Backup-{yyyyMMdd-HHmmss}.sfbak`.

## Envelope (protobuf-net code-first DTOs in `StageFright.Core/Modules/Settings/Backup`)

```csharp
[ProtoContract]
public class BackupEnvelope
{
    [ProtoMember(1)]  public string SchemaVersion { get; set; }        // semver, e.g. "1.0.0" (NFR-002)
    [ProtoMember(2)]  public DateTime GeneratedAt { get; set; }        // UTC
    [ProtoMember(3)]  public string ApplicationVersion { get; set; }

    // Entity collections — ALL ten are REQUIRED for a valid backup (FR-014)
    [ProtoMember(10)] public List<MemberBackupDto> Members { get; set; }
    [ProtoMember(11)] public List<RehearsalBackupDto> Rehearsals { get; set; }          // Includes AttendanceRecords
    [ProtoMember(12)] public List<EventBackupDto> Events { get; set; }                  // Includes ParticipationRecords + EventTypes
    [ProtoMember(13)] public List<FeeBackupDto> Fees { get; set; }
    [ProtoMember(14)] public List<PaymentBackupDto> Payments { get; set; }
    [ProtoMember(15)] public List<TransactionBackupDto> Transactions { get; set; }
    [ProtoMember(16)] public List<CategoryBackupDto> Categories { get; set; }
    [ProtoMember(17)] public SettingsBackupDto Settings { get; set; }
    [ProtoMember(18)] public List<CommitteeMembershipBackupDto> CommitteeMemberships { get; set; }
    [ProtoMember(19)] public List<AuditTrailBackupDto> AuditTrailEntries { get; set; }

    [ProtoMember(30)] public Dictionary<string, int> EntityCounts { get; set; }         // For logging + validation cross-check
}
```

DTOs mirror entity fields 1:1 (including soft-delete fields where present, PKs, timestamps). Field numbers are append-only forever — never reuse or renumber (protobuf forward compatibility, FR-012).

## Export (Backup) contract

`IBackupService.ExportAsync(string filePath)`:

1. Read ALL records of all 10 entity types **including soft-deleted/archived rows** (complete dataset; FR-012).
2. Populate envelope + `EntityCounts`; serialize with protobuf-net; write file.
3. Log structured event with timestamp and per-entity counts (Observability Requirements).

## Import (Restore) contract

`IBackupService.ImportAsync(string filePath)` — strict, atomic, non-destructive:

1. **Deserialize + version check**: unsupported **major** version → `ImportException` with upgrade guidance ("This backup uses schema version X.y.z; this application supports major version N. Please upgrade…"). Same-major/newer-minor accepted (unknown fields ignored by protobuf).
2. **Completeness validation**: every one of the 10 entity collections must be present (Settings non-null; lists non-null — empty list is valid for an org with no data of that type, but a *missing/null* collection fails). Failure → `ImportException`: `"Import file incomplete: missing {entity_type}. Restore from complete backup."`
3. **Pre-import checkpoint** (FR-013): automatically export a backup of the current database; show confirmation dialog with entity counts and checkpoint path; user must explicitly confirm before any write.
4. **Atomic upsert** (FR-015): inside ONE `IUnitOfWork` transaction, for every record: PK exists → UPDATE all non-key fields; PK absent → INSERT. Local records not in the source are left unchanged. Any failure → full rollback, original data untouched.
5. **Post-commit**: audit entry (Action=Import, counts), structured log, success message.

## Error taxonomy

| Condition | Exception | User message |
|-----------|-----------|--------------|
| File unreadable / corrupt protobuf | `ImportException` | "Backup file is corrupted or not a StageFright backup." |
| Unsupported major schema version | `ImportException` | Version + upgrade guidance |
| Missing entity type | `ImportException` | "Import file incomplete: missing {entity_type}. Restore from complete backup." |
| DB failure mid-import | `DataAccessException` (rolled back) | "Restore failed; no changes were made. {context}" |

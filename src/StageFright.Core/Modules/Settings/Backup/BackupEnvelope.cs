using ProtoBuf;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Root protobuf message for a StageFright backup file (.sfbak).
/// The pre-1.2.0 entity collections are REQUIRED for a valid backup (FR-014); collections
/// added in 1.2.0 (JournalEntries, BankReconciliations, ReconciliationLines) are optional so
/// an older file still restores — absent means none of that record type existed.
/// The 1.3.0 organisation and 1.4.0 banking collections (spec 033) are optional in the same way.
/// Field numbers are append-only — never reuse or renumber (forward compatibility, FR-012).
/// </summary>
[ProtoContract]
public class BackupEnvelope
{
    /// <summary>Semver schema version (e.g. "1.0.0"). Major version must match for import.</summary>
    [ProtoMember(1)] public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>UTC timestamp when the backup was generated.</summary>
    [ProtoMember(2)] public DateTime GeneratedAt { get; set; }

    /// <summary>Application version string at time of export.</summary>
    [ProtoMember(3)] public string ApplicationVersion { get; set; } = "1.0.0";

    [ProtoMember(10)] public List<MemberBackupDto>? Members { get; set; }

    /// <summary>Rehearsals with nested AttendanceRecords.</summary>
    [ProtoMember(11)] public List<RehearsalBackupDto>? Rehearsals { get; set; }

    /// <summary>Events with nested EventType and ParticipationRecords.</summary>
    [ProtoMember(12)] public List<EventBackupDto>? Events { get; set; }

    [ProtoMember(13)] public List<FeeBackupDto>? Fees { get; set; }
    [ProtoMember(14)] public List<PaymentBackupDto>? Payments { get; set; }
    [ProtoMember(15)] public List<TransactionBackupDto>? Transactions { get; set; }
    [ProtoMember(16)] public List<AccountBackupDto>? Accounts { get; set; }
    [ProtoMember(17)] public SettingsBackupDto? Settings { get; set; }
    [ProtoMember(18)] public List<CommitteePositionRecordBackupDto>? CommitteePositionRecords { get; set; }
    [ProtoMember(19)] public List<AuditTrailBackupDto>? AuditTrailEntries { get; set; }

    [ProtoMember(20)] public List<AnnualGeneralMeetingBackupDto>? AnnualGeneralMeetings { get; set; }
    [ProtoMember(21)] public List<AgmAttendanceRecordBackupDto>? AgmAttendanceRecords { get; set; }
    [ProtoMember(22)] public List<CommitteeOfficeHolderTypeBackupDto>? CommitteeOfficeHolderTypes { get; set; }
    [ProtoMember(23)] public List<CommitteeTermBackupDto>? CommitteeTerms { get; set; }

    /// <summary>GL journal-entry headers. Added in schema 1.2.0; absent in older files.</summary>
    [ProtoMember(24)] public List<JournalEntryBackupDto>? JournalEntries { get; set; }

    /// <summary>Bank reconciliations (drafts and finalised). Added in schema 1.2.0; absent in older files.</summary>
    [ProtoMember(25)] public List<BankReconciliationBackupDto>? BankReconciliations { get; set; }

    /// <summary>Reconciliation cleared-line join rows. Added in schema 1.2.0; absent in older files.</summary>
    [ProtoMember(26)] public List<ReconciliationLineBackupDto>? ReconciliationLines { get; set; }

    /// <summary>External organisations, archived included. Added in schema 1.3.0; absent in older files.</summary>
    [ProtoMember(27)] public List<ExternalOrganisationBackupDto>? ExternalOrganisations { get; set; }

    /// <summary>Organisation contacts, removed included. Added in schema 1.3.0; absent in older files.</summary>
    [ProtoMember(28)] public List<ExternalOrganisationContactBackupDto>? ExternalOrganisationContacts { get; set; }

    /// <summary>Cheque/other receipts and the opening-balance item on Undeposited Funds. Added in schema 1.4.0; absent in older files.</summary>
    [ProtoMember(29)] public List<ReceivedItemBackupDto>? ReceivedItems { get; set; }

    /// <summary>Per-entity record counts for validation and display. Keys match collection property names.</summary>
    [ProtoMember(30)] public Dictionary<string, int> EntityCounts { get; set; } = new();

    /// <summary>Bank deposits, reversed included. Added in schema 1.4.0; absent in older files.</summary>
    [ProtoMember(31)] public List<BankDepositBackupDto>? BankDeposits { get; set; }

    /// <summary>Deposit-to-received-item join rows. Added in schema 1.4.0; absent in older files.</summary>
    [ProtoMember(32)] public List<BankDepositLineBackupDto>? BankDepositLines { get; set; }
}

# Phase 1 — Data Model: Restore From Backup During First-Run Setup

No EF Core entity changes and **no migration**. Everything below is the `.sfbak` backup contract (protobuf DTOs and their in-memory intermediaries) plus the first-run flow state machine. Field numbers on `[ProtoContract]` types are **append-only** — never reuse or renumber (forward compatibility with existing `.sfbak` files).

`Schema version`: `.sfbak` `BackupEnvelope.SchemaVersion` goes `1.1.0` → `1.2.0`; `Settings.SchemaVersion` stays `1.1.0` (that is the DB schema marker, driven by EF migrations, and this feature adds no migration).

---

## 1. New backup DTOs (one class per file, `StageFright.Core/Modules/Settings/Backup/`)

### `JournalEntryBackupDto` — mirrors `JournalEntry` 1:1

| ProtoMember | Field | Type | Notes |
|---|---|---|---|
| 1 | `Id` | `Guid` | PK |
| 2 | `Type` | `JournalEntryType` (enum) | stored by name via protobuf enum |
| 3 | `Date` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |
| 4 | `Description` | `string?` | |
| 5 | `CreatedAt` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |

No soft-delete fields (immutable GL header — financial exemption). `Transactions` navigation is **not** copied here; the link is carried by `TransactionBackupDto.JournalEntryId` (see §3).

### `BankReconciliationBackupDto` — mirrors `BankReconciliation` 1:1

| ProtoMember | Field | Type | Notes |
|---|---|---|---|
| 1 | `Id` | `Guid` | PK |
| 2 | `AccountId` | `Guid` | FK → `Account` |
| 3 | `StatementDate` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |
| 4 | `StatementClosingBalance` | `decimal` | |
| 5 | `OpeningBalance` | `decimal` | |
| 6 | `Status` | `ReconciliationStatus` (enum) | Draft / Finalised |
| 7 | `FinalisedAt` | `DateTime?` | plain `[ProtoMember]` (protobuf-net default) |
| 8 | `Notes` | `string?` | |
| 9 | `IsDeleted` | `bool` | drafts only |
| 10 | `DeletedAt` | `DateTime?` | plain `[ProtoMember]` (protobuf-net default) |
| 11 | `DeletedBy` | `string?` | |
| 12 | `CreatedAt` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |
| 13 | `UpdatedAt` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |

`Lines` navigation not copied here — carried as its own collection.

### `ReconciliationLineBackupDto` — mirrors `ReconciliationLine` 1:1

| ProtoMember | Field | Type | Notes |
|---|---|---|---|
| 1 | `Id` | `Guid` | PK |
| 2 | `ReconciliationId` | `Guid` | FK → `BankReconciliation` |
| 3 | `TransactionId` | `Guid` | FK → `Transaction` |
| 4 | `CreatedAt` | `DateTime` | plain `[ProtoMember]` (protobuf-net default) |

No soft-delete fields (hard-removed while its parent is a draft; implicitly hidden once the parent is soft-deleted).

---

## 2. `SettingsBackupDto` — twelve new fields

Existing members `1–9, 11–18` unchanged. New members at previously-free numbers:

| ProtoMember | Field | Type | Source (`Settings`) | Default when absent from an older file |
|---|---|---|---|---|
| 10 | `FinancialYearStartMonth` | `int` | `FinancialYearStartMonth` | `7` |
| 19 | `FinancialYearStartDay` | `int` | `FinancialYearStartDay` | `1` |
| 20 | `CurrencyCode` | `string` | `CurrencyCode` | `"AUD"` |
| 21 | `ClosedThroughDate` | `DateTime?` | `ClosedThroughDate` | `null` |
| 22 | `InceptionDate` | `DateTime?` | `InceptionDate` | `null` |
| 23 | `IsTaxApplicable` | `bool` | `IsTaxApplicable` | `false` |
| 24 | `TaxRate` | `decimal?` | `TaxRate` | `null` |
| 25 | `AnnualFeeTaxCode` | `TaxCode?` (enum) | `AnnualFeeTaxCode` | `null` |
| 26 | `AttendanceFeeTaxCode` | `TaxCode?` (enum) | `AttendanceFeeTaxCode` | `null` |
| 27 | `TaxEntryMode` | `TaxEntryMode` (enum) | `TaxEntryMode` | `Inclusive` |
| 28 | `LanguageCode` | `string?` | `LanguageCode` | `null` |
| 29 | `ShowParticipationGraphs` | `bool` | `ShowParticipationGraphs` | `true` |

All members use the plain `[ProtoMember]` default, matching the existing 20 `*BackupDto` types (protobuf-net's default `DateTime` form is a BCL tick+kind representation, already culture- and endian-independent). Restore maps every field straight onto the `Settings` entity; the defaults above are the entity's own property initialisers, so an older `.sfbak` produces exactly the same `Settings` row it does today (spec Edge Case "Older backup missing newer data").

---

## 3. `FeeBackupDto` / `TransactionBackupDto` — missing columns

### `FeeBackupDto` (+1)

| ProtoMember | Field | Type | Notes |
|---|---|---|---|
| 10 | `TaxCode` | `TaxCode?` (enum) | from `Fee.TaxCode`; `null` in older files → tax-exempt fee, unchanged behaviour |

### `TransactionBackupDto` (+2)

| ProtoMember | Field | Type | Notes |
|---|---|---|---|
| 12 | `TaxCode` | `TaxCode?` (enum) | from `Transaction.TaxCode` |
| 13 | `JournalEntryId` | `Guid?` | from `Transaction.JournalEntryId`; restores the header↔line grouping |

Restore maps these directly; an older file leaves them `null`, i.e. the current (lossy) behaviour becomes the explicit fallback rather than silent loss.

---

## 4. `BackupEnvelope` — three new collections

Existing members `1–3, 10–23, 30` unchanged. New:

| ProtoMember | Member | Type |
|---|---|---|
| 24 | `JournalEntries` | `List<JournalEntryBackupDto>?` |
| 25 | `BankReconciliations` | `List<BankReconciliationBackupDto>?` |
| 26 | `ReconciliationLines` | `List<ReconciliationLineBackupDto>?` |

`EntityCounts` (member 30) gains keys `"JournalEntries"`, `"BankReconciliations"`, `"ReconciliationLines"`. `MapToEnvelope` always writes every key (even at 0); `DeserializeAndValidate` normalises the three new `null` collections to `[]`, same as the existing ones.

**Completeness gate (`ValidateCompleteness`)**: unchanged set of *required* keys (`Members`, `CommitteePositionRecords`, `Rehearsals`, `AnnualGeneralMeetings`, `CommitteeOfficeHolderTypes`, `CommitteeTerms`, `Events`, `Fees`, `Payments`, `Transactions`, `Accounts`/`Categories`, `AuditTrailEntries`). The three new keys are **optional** — their absence in a pre-1.2.0 file is normal, not a failure.

---

## 5. `BackupSnapshot` — three new lists

In-memory intermediary between `BackupRepository` and `BackupService`. Add:

- `IReadOnlyList<JournalEntry> JournalEntries` (default `Array.Empty<JournalEntry>()`)
- `IReadOnlyList<BankReconciliation> BankReconciliations` (default empty)
- `IReadOnlyList<ReconciliationLine> ReconciliationLines` (default empty)

**Read** (`BackupRepository.GetFullSnapshotAsync`): `JournalEntries` — plain `AsNoTracking()` (no query filter). `BankReconciliations` and `ReconciliationLines` — `IgnoreQueryFilters().AsNoTracking()` so soft-deleted drafts are included (FR-011/FR-016).

**Upsert** (`BackupRepository.UpsertSnapshotAsync`) — FK-safe order, extending the current sequence:
`Members → AGMs → CommitteeOfficeHolderTypes → CommitteeTerms → CommitteePositionRecords → AgmAttendanceRecords → Rehearsals → AttendanceRecords → EventTypes → Events → ParticipationRecords → Accounts → **JournalEntries** → Fees → Payments → Transactions → **BankReconciliations** → **ReconciliationLines** → Settings → AuditTrailEntries`.
Rationale: `Transaction.JournalEntryId` → `JournalEntries` first; `ReconciliationLine` → both `BankReconciliations` and `Transactions` first.

---

## 6. `BackupVerificationResult` — new value type (`.../Backup/`)

The output of the post-write self-check (FR-021 – FR-024). Also the shape the create-backup UI renders as "verified" / "failed".

| Member | Type | Meaning |
|---|---|---|
| `Passed` | `bool` | true only when read-back succeeded and every count matched |
| `Discrepancies` | `IReadOnlyList<string>` | human-readable mismatches (empty when `Passed`); e.g. `"Members: file records 128, live data has 129"` |
| `FilePath` | `string` | the written file (kept on disk even when `!Passed`, for diagnosis) |

`BackupService` throws `BackupVerificationException` (carrying the discrepancy list in its message) when `!Passed`; the `Passed` result is returned on success. `BackupManifest` (unchanged) remains the restore-side summary type — `Passed`'s counts are computed with the same helper that builds `BackupManifest.EntityCounts`.

---

## 7. Version comparison model (`BackupSchema`, new static class in `StageFright.Core`)

| Member | Type | Value |
|---|---|---|
| `CurrentSchemaVersion` | `string` (semver) | `"1.2.0"` |
| `IsRestorable(string fileVersion)` | `bool` | `false` if `fileVersion` is empty/unparseable **or** `fileVersion > CurrentSchemaVersion` on any component; `true` otherwise (equal or older) |

`BackupService.ValidateVersion` calls `BackupSchema.IsRestorable`. Reject → `ImportException` with the "update the application and retry" message (`Validation_Backup_UnsupportedSchemaVersion`, reworded). Older/equal → proceed; EF startup migration handles an older restored DB.

---

## 8. First-run flow — state machine

```
                        ┌─────────────────────────────────────────────┐
launch (no startup err, │  App.razor.cs                               │
 setup incomplete) ────▶ │  lang pref recorded? ── no ──▶ /language-select
                        │                     └─ yes ──▶ /first-run-restore
                        └─────────────────────────────────────────────┘

/language-select ──confirm──▶ /first-run-restore        (debug seed path: ──▶ /dashboard, unchanged)

/first-run-restore
   checkbox OFF ──Continue──▶ /setup  (existing SetupWizard, unchanged)
   checkbox ON
      pick .sfbak (<InputFile>) ──▶ GetManifestAsync
         invalid / corrupt / newer version ──▶ error shown, DB untouched, stay on screen
         valid ──▶ show summary (per-type counts, created date, originating version) + Confirm / Cancel
            Cancel ──▶ back to checkbox state, nothing changed  ──Continue──▶ /setup
            Confirm ──▶ ImportAsync
                        recovery copy written to IRecoveryCopyStore dir (copy of seeded default DB)
                        atomic PK-upsert transaction
                        audit entry (AuditAction.Import) in the restored DB
               success ──▶ /restart-required   (terminal; only "Close and reopen")
               failure ──▶ error shown, pre-restore DB intact  ──▶ /setup still available
```

Next launch after a successful restore: `Settings` row exists → `IsSetupCompleteAsync()` true → normal routing to `/dashboard`; `LanguageProvider` resolves `Settings.LanguageCode` from the restored data first, so the restored language takes effect.

**Settings-path restore** (`BackupRestoreTab`) is unchanged in shape but gains: the native Save dialog + default filename for *create*, the unencrypted-file notice, the `BackupVerificationResult` outcome, and the same `/restart-required` navigation after a successful *restore*.

---

## 9. Validation rules (from the spec's requirements)

| Rule | Where enforced |
|---|---|
| Backup file unreadable / not a `.sfbak` → reject, no DB change (FR-004) | `BackupService.DeserializeAndValidate` → `ImportException` |
| File `SchemaVersion` newer than app (incl. newer same-major) → reject (FR-019) | `BackupSchema.IsRestorable` |
| Completeness: a *required* legacy collection key missing → reject (FR-004) | `BackupService.ValidateCompleteness` |
| Restore is all-or-nothing; partial failure leaves pre-restore state (FR-005) | `IUnitOfWork.ExecuteInTransactionAsync` (existing) |
| Pre-restore recovery copy written first, every restore (FR-015) | `BackupService.ImportAsync` + `IRecoveryCopyStore` |
| Post-write read-back + count match against source & file self-counts (FR-021–024) | `BackupService` create path → `BackupVerificationResult` / `BackupVerificationException` |
| Default filename = `<sanitised org> backup <yyyy-MM-dd>.sfbak`, valid chars only, fixed fallback when org name blank (FR-010) | `BackupFileNameBuilder.Build` (pure, unit-tested) |
| Backup file unencrypted, no password prompt; user shown a notice (FR-020) | `BackupRestoreTab` UI + localisation key |
| Create-backup and restore each audited (FR-017) | `BackupService.ExportAsync` (`AuditAction.Export`) / `ImportAsync` (`AuditAction.Import`) |
| Restore never blocks the UI thread; progress advances (Edge Cases) | UI runs `ImportAsync` on `Task.Run` with a `Progress<>` indicator, mirroring the sample-data seeding overlay |

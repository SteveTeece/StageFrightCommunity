# Contract — `.sfbak` protobuf wire format (v1.2.0)

`BackupEnvelope` is the root `[ProtoContract]` message. **Field numbers are append-only** — never reuse, never renumber. A restore of any file written by a build at or before this version's app must still succeed.

Full field tables: `data-model.md` §1–§5. This file is the numbering + compatibility contract.

---

## `SchemaVersion`

- Written value: `"1.2.0"` (was `"1.1.0"`).
- Restore acceptance: `BackupSchema.IsRestorable(file.SchemaVersion)` — accept when equal or older on every semver component; reject when newer on any component (a newer same-major build included) or unparseable/empty. Rejection message key: `Validation_Backup_UnsupportedSchemaVersion` (reworded to "update the application and retry").
- `ApplicationVersion` (member 3) stays a display-only string; not a compatibility axis.

## New `BackupEnvelope` members

| # | Member | Type |
|---|---|---|
| 24 | `JournalEntries` | `List<JournalEntryBackupDto>?` |
| 25 | `BankReconciliations` | `List<BankReconciliationBackupDto>?` |
| 26 | `ReconciliationLines` | `List<ReconciliationLineBackupDto>?` |

Existing members `1–3`, `10–23`, `30` are untouched. Member 30 `EntityCounts` gains keys `"JournalEntries"`, `"BankReconciliations"`, `"ReconciliationLines"`; `MapToEnvelope` always writes every key even at `0`.

## New DTO `[ProtoMember]` numbers

| DTO | New members |
|---|---|
| `JournalEntryBackupDto` (new type) | `1..5` |
| `BankReconciliationBackupDto` (new type) | `1..13` |
| `ReconciliationLineBackupDto` (new type) | `1..4` |
| `SettingsBackupDto` (existing 1–9, 11–18) | `10`, `19`, `20`, `21`, `22`, `23`, `24`, `25`, `26`, `27`, `28`, `29` |
| `FeeBackupDto` (existing 1–9) | `10` — `TaxCode` |
| `TransactionBackupDto` (existing 1–11) | `12` — `TaxCode`; `13` — `JournalEntryId` |

`DateTime` / `DateTime?` on the **new** members use `[ProtoMember(n, DataFormat = DataFormat.WellKnown)]` (UTC `google.protobuf.Timestamp`). `DateTime` members on **existing** DTOs keep their current default wire format — changing them would break older-file reads; the `CrossPlatformRoundTripTests` guard proves the existing format round-trips losslessly across time zones.

## Old-file compatibility rules

1. **Absent new collections** (members 24–26 not in the bytes) → protobuf-net deserialises `null` → `DeserializeAndValidate` normalises to `[]` → restore proceeds with zero of those records.
2. **Absent new `SettingsBackupDto` members** → each maps to the `Settings` entity property's own initialiser default (see `data-model.md` §2), so a pre-1.2.0 file yields exactly the `Settings` row it does today.
3. **Absent `FeeBackupDto.TaxCode` / `TransactionBackupDto.TaxCode` / `JournalEntryId`** → `null`, i.e. today's (lossy) behaviour becomes the explicit documented fallback.
4. **Completeness gate** (`BackupService.ValidateCompleteness`) checks only the pre-existing required keys. `"JournalEntries"`, `"BankReconciliations"`, `"ReconciliationLines"` are **not** added to that gate — their absence is normal for an older file (spec Edge Case "Older backup missing newer data"). This mirrors the existing `"Accounts"` / `"Categories"` dual-key tolerance.
5. **`EntityCounts` present but new keys missing** → treated as `0` for that type; no failure.

## Round-trip invariants (asserted by tests)

- Re-reading a freshly written v1.2.0 file yields collection lengths equal to that file's `EntityCounts` values (internal consistency, FR-023).
- Those counts equal the counts of the `BackupSnapshot` captured for that backup, archived rows included (FR-022).
- A create → restore cycle reproduces every record field-for-field, same archived/active state, same counts per type (FR-016), independent of host OS, culture, separators, calendar, and time zone (FR-013 / FR-014).

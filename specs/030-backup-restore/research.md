# Phase 0 — Research: Restore From Backup During First-Run Setup

Decisions the spec left open, and what the existing codebase already settles. Every `NEEDS CLARIFICATION` from the spec was resolved in the 2026-09-07 clarification session (encryption, newer-same-major rejection, always-write recovery copy, no time target) and is not re-opened here.

---

## D1 — Native Save dialog for choosing the backup destination and filename

**Decision**: Add `CommunityToolkit.Maui` and use `FileSaver.Default.SaveAsync(suggestedFileName, stream, ct)` behind a new Core seam `IBackupDestinationPicker`, implemented in `StageFright.App` as `MauiBackupDestinationPicker`. The seam takes the serialised backup as a `Stream` plus the suggested filename, shows the OS-native Save As dialog pre-filled with that name, writes the file, and returns the chosen absolute path (or a "cancelled" outcome).

**Rationale**:
- The app has **no** save-dialog mechanism today — the existing Settings backup writes straight to `Environment.SpecialFolder.MyDocuments` with a machine-generated name, and every "give the user a file" path elsewhere (`ReportViewer`, `AgmAttendanceReportPrinter`) writes to `Path.GetTempPath()` then `Process.Start(UseShellExecute=true)`. FR-009 / FR-010 and the spec's "operating system's native save dialog" assumption require a real Save As.
- MAUI Essentials has `FilePicker` (open) but **no** save picker. `CommunityToolkit.Maui` is the standard, .NET-team-maintained answer; `FileSaver` supports Windows and Mac Catalyst, and `SaveAsync(suggestedFileName, …)` pre-fills the dialog name — an exact fit for FR-010's default filename.
- Overwrite handling, folder navigation, and "file exists" prompts are delegated to that dialog (spec Edge Case "Destination filename already exists").

**Alternatives considered**:
- *Hand-rolled platform pickers* — Windows `Windows.Storage.Pickers.FileSavePicker` (needs `InitializeWithWindow` HWND interop) + Mac Catalyst `UIDocumentPickerViewController`. Rejected: more code, more `#if` platform branching in `StageFright.App`, more to test, no capability the toolkit lacks.
- *Keep writing to a fixed folder (My Documents / Downloads)* — rejected: violates FR-009 ("choosing both the destination location and the filename").
- *`FolderPicker` (pick a folder, compose the path ourselves)* — `CommunityToolkit.Maui` offers this too, but it loses the "confirm/rename the filename" step and the native overwrite prompt; `FileSaver` is the better match.

**Restore-side note**: the file *selection* for restore stays on the existing Blazor `<InputFile accept=".sfbak">` → copy-to-temp pattern already in `BackupRestoreTab`. It is cross-platform, needs no new dependency, and the first-run screen reuses it verbatim.

---

## D2 — Where the first-run restore choice lives

**Decision**: A **new dedicated pre-wizard screen** `FirstRunRestoreScreen` at `@page "/first-run-restore"`, shown after `/language-select` and before `/setup`. It carries the FR-001 checkbox ("Restore from a backup file instead of setting up manually"). Unticked + Continue → `/setup`. Ticked → `<InputFile>` → contents summary (`BackupManifest`) → confirm/cancel → restore → `/restart-required`.

Routing changes:
- `App.razor.cs`: when `!IsSetupCompleteAsync()` and no startup error, the target becomes `/language-select` (no recorded language preference) **or `/first-run-restore`** (preference already recorded) — never `/setup` directly. This keeps the restore option reachable on a second launch that already chose a language but never finished setup.
- `FirstRunLanguageScreen.HandleConfirmAsync`: navigates to `/first-run-restore` instead of `/setup`. The debug sample-data path is unchanged (still seeds and goes straight to `/dashboard`).

**Rationale**: The spec Assumptions state the restore option "appears as a dedicated choice … consistent with the pre-wizard screen pattern established for the language selection." A separate screen keeps `FirstRunLanguageScreen` single-purpose and mirrors spec 029's structure. The checkbox is a literal Verbatim Constraint ("use a checkbox control") — a `<RadzenSwitch>` would violate it, so this is one deliberate exception to the CLAUDE.md toggle rule, exactly as the wizard's own Light/Dark `<select>` is.

**Alternatives considered**: adding the checkbox onto `FirstRunLanguageScreen` — rejected: mixes two unrelated first-run decisions on one screen and complicates that screen's debug-seed branch.

---

## D3 — Enforcing the mandatory restart (FR-007)

**Decision**: On a successful restore (first-run or Settings), navigate to a terminal `RestartRequiredScreen` at `@page "/restart-required"` that renders outside the shell nav and offers **no** "Continue" / "Go to dashboard" action — only "Close and reopen the application". The app does not relaunch itself.

**Rationale**: After a restore, cached configuration, the resolved culture, `MoneyFormatter`'s configured currency, the first-run-complete flag, and `ILanguagePreferenceStore` are all pre-restore in-memory state (spec Edge Cases). A blocking terminal screen is the simplest way to guarantee the user cannot "keep clicking" into a half-restored mixture. On the next launch `App.razor.cs` sees a `Settings` row (restored) → `IsSetupCompleteAsync()` is true → normal routing to `/dashboard`; `LanguageProvider`'s ladder (explicit `Settings.LanguageCode` first) makes the restored language win with no extra code (spec Edge Case "Language recorded outside the database").

**Alternatives considered**: a dismissible toast/alert then continue — rejected: does not satisfy "MUST NOT proceed … on pre-restore in-memory state". Auto-relaunch — rejected by the spec ("not reliably possible across all supported desktop platforms").

---

## D4 — Post-write backup verification (FR-021 – FR-024)

**Decision**: `BackupService` performs the read-back check itself, immediately after the file is written and before reporting success:
1. Keep the per-record-type counts computed from the `BackupSnapshot` that was read at the start of the create (this is "the live database as captured for that backup").
2. Re-open the written file from disk and deserialise it into a fresh `BackupEnvelope`.
3. Recompute counts from the re-read envelope's actual collection lengths.
4. Assert three ways: re-read collection lengths == re-read `EntityCounts` (file internally consistent); re-read `EntityCounts` == snapshot counts (file faithful to source); creation date & originating version parse and are present.
5. On any failure throw the new `BackupVerificationException` (message: the file must not be relied upon). The file is left on disk for diagnosis (spec Edge Case). On success return a `BackupVerificationResult { Passed = true }`.

**Rationale**: Mirrors the spec's "Backup summary … also computed straight after a backup and checked against the live data". Reusing the same count-computation code for the restore-confirmation manifest and the post-write check means one code path, so the numbers a treasurer sees at hand-over time are provably the numbers a restore will show. An exception (not a bool return threaded everywhere) matches the codebase idiom (`ImportException`).

**Alternatives considered**: hashing the file and re-hashing on restore — rejected: proves the bytes are stable but not that the *counts* are right or that the file is *readable as a backup*; the spec specifically wants a semantic check.

---

## D5 — Version compatibility: reject newer, accept older (FR-019)

**Decision**: Replace `ValidateVersion`'s "major must equal `1`" test with a full semantic-version comparison of the file's `SchemaVersion` against a single `BackupSchema.CurrentSchemaVersion` constant in `StageFright.Core` (bumped to `"1.2.0"` by this feature):
- file version **> current** on any component (major, minor, or patch — a newer same-major build included) → reject outright with an "update the application and retry" message; no DB change.
- file version **≤ current** → accept; the normal EF Core startup migration brings older restored data forward on next launch.
- unparseable / empty version → reject (unchanged).

**Rationale**: The clarification explicitly widened rejection to "a newer build of the same major version … which may carry settings or record types this build does not recognise". The comparison is against a compiled constant, not `AppInfo.Current.Version`, so it is deterministic, host-independent (FR-013/FR-014), and unit-testable without a MAUI runtime.

**Alternatives considered**: comparing marketing `ApplicationVersion` — rejected: that string is currently a hardcoded `"1.0.0"` placeholder and is a display value, not a compatibility axis. Keeping the major-only check — rejected: fails the clarified FR-019.

---

## D6 — Backup completeness: three record types, twelve settings fields, and three missing DTO columns

**Decision**: Bring the `.sfbak` format up to the data model:

*New record types* — add `JournalEntryBackupDto`, `BankReconciliationBackupDto`, `ReconciliationLineBackupDto` (fields mirrored 1:1 from the entities), new `BackupEnvelope` collections at append-only `[ProtoMember]` numbers `24` / `25` / `26`, new `BackupSnapshot` lists, new `EntityCounts` keys, and read + upsert coverage in `BackupRepository`. `BankReconciliation` / `ReconciliationLine` are read with `IgnoreQueryFilters()` (soft-deletable via the parent's filter); `JournalEntry` has no filter. Upsert order is FK-safe: `JournalEntries` before `Transactions`; `Accounts` and `Transactions` before `BankReconciliations`/`ReconciliationLines`; `ReconciliationLines` last.

*Settings fields* — add twelve `[ProtoMember]`s to `SettingsBackupDto` at free numbers `10, 19–29`: `FinancialYearStartMonth`, `FinancialYearStartDay`, `CurrencyCode`, `ClosedThroughDate`, `InceptionDate`, `IsTaxApplicable`, `TaxRate`, `AnnualFeeTaxCode`, `AttendanceFeeTaxCode`, `TaxEntryMode`, `LanguageCode`, `ShowParticipationGraphs`.

*Missing DTO columns found during investigation* — `FeeBackupDto` is missing `Fee.TaxCode`; `TransactionBackupDto` is missing `Transaction.TaxCode` and `Transaction.JournalEntryId`. Add all three (append-only member numbers). These are silent financial data loss today and are squarely in FR-011 / FR-016.

*Guard* — a reflection-based `BackupDtoFieldParityTests` asserts every persisted-entity property has a corresponding backup-DTO property (with an explicit allow-list for genuinely derived/navigation members), so a future entity field cannot silently fall out of the backup again (SC-008).

**Rationale**: The spec Background calls out the three record types and the settings drift; the investigation showed the rot is slightly worse (three DTO columns). One systematic pass plus a parity test closes it and keeps it closed.

**Older-file tolerance**: the completeness gate (`ValidateCompleteness`) keeps only the **pre-existing** required keys. The new keys are optional — a pre-1.2.0 file simply has no `JournalEntries`/`BankReconciliations`/`ReconciliationLines` and no new settings values, and restore succeeds with those items at their defaults (spec Edge Case "Older backup missing newer data"), exactly like today's `Accounts`/`Categories` dual-key tolerance. protobuf-net already deserialises an absent collection as `null`, and `DeserializeAndValidate` already normalises those to `[]`.

**Alternatives considered**: adding the new keys to the completeness gate — rejected: would make every existing `.sfbak` fail to restore, contradicting FR-019's "older version accepted".

---

## D7 — Pre-restore recovery copy location (FR-015)

**Decision**: Add `IRecoveryCopyStore` (Core contract) returning a stable directory, implemented in `StageFright.App` as `FileSystem.AppDataDirectory/recovery`. `BackupService.ImportAsync` writes the pre-restore recovery copy there (filename `StageFright-Recovery-<yyyyMMdd-HHmmss>.sfbak`) via the existing `ExportAsync` path, for **every** restore including first-run (where it is a copy of the freshly-seeded default database) — one code path.

**Rationale**: The current `GenerateCheckpointPath` writes next to the import file, and in practice the import file is always a temp copy (`Path.GetTempPath()`), so recovery copies land in a directory the OS may clean. FR-015 + the Edge Case "Restore interrupted … the pre-restore recovery copy must still be available" want it somewhere durable and findable. `BackupService` lives in `StageFright.Core` and cannot reference `FileSystem.AppDataDirectory` directly, so a thin injected seam (matching `ILanguagePreferenceStore`'s shape) is the minimal change; tests supply a temp-dir fake.

**Alternatives considered**: passing the recovery directory as a parameter to `ImportAsync` — rejected: pushes a platform concern into every caller. Keeping the next-to-import-file behaviour — rejected: temp dir is not durable enough for the interrupted-restore case.

---

## D8 — Cross-platform / cross-locale portability proof (FR-013 / FR-014, US3)

**Decision**: A new `CrossPlatformRoundTripTests` integration test creates a backup from a representative multi-year dataset, then restores it into a fresh in-memory SQLite database while, for the restore half, `CultureInfo.CurrentCulture` / `CurrentUICulture` are swapped to a comma-decimal, non-Gregorian-ish locale and `TimeZoneInfo` handling is exercised, and asserts the restored rows are field-for-field identical to the source (records, values, archived state, counts). It runs on the project's normal test platform and fails on any divergence.

**Rationale**: `protobuf-net` binary is already culture-invariant and endian-defined, so text/number formatting cannot drift — but `DateTime.Kind` fidelity across time zones is the real risk, and the spec asks for an explicit test rather than an assertion of safety. New `DateTime` members on the new DTOs use `DataFormat.WellKnown` (UTC `google.protobuf.Timestamp`); existing `DateTime` members keep their current wire format so pre-1.2.0 files still read, and the round-trip test is the guard that this is actually lossless. Per the CLAUDE.md note about `bunit-culture-mutation-test-race`, the culture-mutating test is placed in `StageFright.Integration.Tests` (not `UI.Tests`) and restores the ambient culture in a `finally`.

**Alternatives considered**: forcing every `DateTime` to `WellKnown` now — rejected: changes the wire format of fields in existing `.sfbak` files, breaking older-file restore.

---

## D9 — Audit trail for backup creation (FR-017)

**Decision**: `BackupService.ExportAsync` writes an `AuditTrailEntry` with `AuditAction.Export` (the enum value already exists and is currently unused) after verification passes. Restore already logs `AuditAction.Import`; that entry is written inside the restore transaction so it lands in the restored dataset and is "recorded against the restored database once it is in use" (FR-017).

**Rationale**: One-line addition; the enum and `IAuditTrailService` are already injected into `BackupService`.

---

## D10 — Unencrypted-file notice (FR-020)

**Decision**: The create-backup flow (Settings tab and, if ever reachable, first-run) shows a brief inline notice before/at save time: the file is unencrypted and contains member personal data and financial history. No password prompt anywhere. Text is a new localisation key in `SettingsResource`. File-level encryption is explicitly out of scope and noted as a future enhancement.

**Rationale**: Direct from the clarified FR-020; consistent with the app's unencrypted local database.

---

## Documentation impact (per CLAUDE.md Spec & Docs Workflow)

- `docs/ARCHITECTURE.md` — the backup paragraph ("`BackupRepository` … entity-specific query shapes") and the entity-count wording ("all 13 entity types") go stale → update to 16 and note first-run reachability.
- `docs/SETUP.md` — the first-run description ("first-run detection redirects the UI to the `/setup` wizard") must mention the `/first-run-restore` screen and the restore-then-restart path; the "start over" section may note that a recovery copy is written under `FileSystem.AppDataDirectory/recovery`.
- The living spec drafts `capabilities/app-host/spec.md` and `capabilities/data-access/spec.md` describe first-run routing and `BackupRepository` behaviour; they are `[DRAFT]` and out of the stock spec-kit doc set — not updated here, but the routing/upsert changes are noted for a future living-spec sync.
- No `specs/016`, `028`, or `029` behaviour is changed; their mentions of `.sfbak` remain accurate.
